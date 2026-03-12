using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GlobalModeControl : MonoBehaviour
{
    public static GlobalModeControl Instance { get; private set; }
    public bool IsAuto { get; private set; } = true;

    
    public static bool IsAutoMode => Instance != null && Instance.IsAuto;

    [Header("Кнопки АВТ / РУЧ")]
    [SerializeField] private Button _btnAuto;
    [SerializeField] private Button _btnManual;

    [Header("Метка режима (необязательно)")]
    [SerializeField] private TextMeshProUGUI _labelMode;

    [Header("Цвета")]
    public Color colorActive = new Color(0.1f, 0.75f, 0.2f, 1f);
    public Color colorInactive = new Color(0.25f, 0.25f, 0.25f, 1f);

    private bool _busy = false;

    void Awake()
    {
        Instance = this;
        if (_btnAuto) _btnAuto.transition = Selectable.Transition.None;
        if (_btnManual) _btnManual.transition = Selectable.Transition.None;
    }

    void Start()
    {
        _btnAuto?.onClick.AddListener(ClickAuto);
        _btnManual?.onClick.AddListener(ClickManual);
        Paint();
    }

    public void ClickAuto()
    {
        if (IsAuto || _busy) return;
        IsAuto = true;
        Paint();
        Send("AUTO_ON", ok => { if (!ok) IsAuto = false; _busy = false; Paint(); });
    }

    public void ClickManual()
    {
        if (!IsAuto || _busy) return;
        IsAuto = false;
        Paint();
        Send("AUTO_OFF", ok => { if (!ok) IsAuto = true; _busy = false; Paint(); });
    }

    
    public void ForceState(bool isAuto)
    {
        IsAuto = isAuto;
        _busy = false;
        Paint();
    }

    private void Send(string cmd, System.Action<bool> done)
    {
        var ard = ArduinoController_Connect.Instance;
        if (ard == null || !ard.isConnected)
        {
            IsAuto = !IsAuto;
            Paint();
            return;
        }
        _busy = true;
        Paint();
        ard.SendCommand(cmd, done);
    }

    private void Paint()
    {
        PaintBtn(_btnAuto, canClick: !IsAuto && !_busy);
        PaintBtn(_btnManual, canClick: IsAuto && !_busy);
        if (_labelMode) _labelMode.text = IsAuto ? "АВТ" : "РУЧ";
    }

    private void PaintBtn(Button btn, bool canClick)
    {
        if (!btn) return;
        btn.interactable = canClick;
        var img = btn.GetComponent<Image>();
        if (img) img.color = canClick ? colorActive : colorInactive;
    }
}