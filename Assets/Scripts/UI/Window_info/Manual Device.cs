using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ManualDeviceControl : MonoBehaviour
{
    public enum DeviceType { WaterPump, ReagentPump, Heater, DrainValve }

    [Header("Тип устройства")]
    public DeviceType Device = DeviceType.WaterPump;

    [Header("Кнопки панели")]
    [SerializeField] private Button _btnMinus;
    [SerializeField] private Button _btnPlus;
    [SerializeField] private Button _btnSet;
    [SerializeField] private Button _btnClose;

    [Header("Текст значения внутри панели")]
    [SerializeField] private TextMeshProUGUI _labelValue;

    [Header("Индикатор над панелью")]
    [SerializeField] private Image _indicatorBg;
    [SerializeField] private TextMeshProUGUI _indicatorLabel;

    [Header("Цвета")]
    public Color colorOn = new Color(0.1f, 0.85f, 0.1f, 1f);
    public Color colorOff = new Color(0.85f, 0.15f, 0.15f, 1f);
    public Color colorBtnOn = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Color colorBtnOff = new Color(0.40f, 0.40f, 0.40f, 1f);

    private static readonly int[] StepsPump = { 0, 50, 70, 100 };
    private static readonly int[] StepsBinary = { 0, 100 };

    private int[] Steps => (Device == DeviceType.Heater || Device == DeviceType.DrainValve)
        ? StepsBinary : StepsPump;

    private int _wantIdx = 0;
    private int _appliedIdx = 0;
    private bool _busy = false;
    private bool _prevAuto = true;

    // =========================================================================
    void Awake()
    {
        NoTransition(_btnMinus);
        NoTransition(_btnPlus);
        NoTransition(_btnSet);
    }

    void Start()
    {
        _btnMinus?.onClick.AddListener(ClickMinus);
        _btnPlus?.onClick.AddListener(ClickPlus);
        _btnSet?.onClick.AddListener(ClickSet);
        _btnClose?.onClick.AddListener(ClickClose);

        _prevAuto = GlobalModeControl.Instance?.IsAuto ?? true;
        Redraw();
    }

    void Update()
    {
        bool g = GlobalModeControl.Instance?.IsAuto ?? true;
        if (g == _prevAuto) return;
        _prevAuto = g;
        if (!g) _wantIdx = _appliedIdx;
        Redraw();
    }

    // ─── Кнопки ──────────────────────────────────────────────────────────────
    private void ClickMinus()
    {
        if (Blocked() || _wantIdx == 0) return;
        _wantIdx--;
        Redraw();
    }

    private void ClickPlus()
    {
        if (Blocked() || _wantIdx >= Steps.Length - 1) return;
        _wantIdx++;
        Redraw();
    }

    private void ClickSet()
    {
        if (Blocked() || _wantIdx == _appliedIdx) return;

        var ard = ArduinoController_Connect.Instance;
        if (ard == null || !ard.isConnected) return;

        _busy = true;
        Redraw();

        string cmd = BuildCmd(_wantIdx);
        ard.SendCommand(cmd, ok =>
        {
            if (ok) _appliedIdx = _wantIdx;
            else _wantIdx = _appliedIdx;
            _busy = false;
            Redraw();
        });
    }

    private void ClickClose()
    {
        if (_busy) return;
        if (_appliedIdx > 0)
            ArduinoController_Connect.Instance?.SendRaw(BuildCmd(0));
        _appliedIdx = 0;
        _wantIdx = 0;
        Redraw();

        // Скрываем саму панель
        gameObject.SetActive(false);
    }

    // ─── ★ Публичный метод для CloseAllPanels ────────────────────────────────
    /// <summary>
    /// Выключить устройство и скрыть панель.
    /// Если устройство занято (_busy), закрытие откладывается до завершения команды.
    /// </summary>
    public void ClosePanel()
    {
        if (_busy)
        {
            // Ждём ответа Arduino, затем закрываем
            StartCoroutine(CloseWhenFree());
            return;
        }
        ClickClose();
    }

    private System.Collections.IEnumerator CloseWhenFree()
    {
        while (_busy) yield return null;
        ClickClose();
    }

    // ─── Вызов из ArduinoDataReceiver (CTRL-пакет) ───────────────────────────
    public void OnCtrlReceived(int value)
    {
        _appliedIdx = value > 0 ? Steps.Length - 1 : 0;
        if (GlobalModeControl.Instance?.IsAuto ?? true)
            _wantIdx = _appliedIdx;
        Redraw();
    }

    // ─── Отрисовка ───────────────────────────────────────────────────────────
    private void Redraw()
    {
        bool isAuto = GlobalModeControl.Instance?.IsAuto ?? true;
        bool can = !isAuto && !_busy;

        PaintBtn(_btnMinus, can && _wantIdx > 0);
        PaintBtn(_btnPlus, can && _wantIdx < Steps.Length - 1);
        PaintBtn(_btnSet, can && _wantIdx != _appliedIdx);

        int displayPct = isAuto ? Steps[_appliedIdx] : Steps[_wantIdx];
        if (_labelValue) _labelValue.text = displayPct + "%";

        bool on = _appliedIdx > 0;
        if (_indicatorBg) _indicatorBg.color = on ? colorOn : colorOff;
        if (_indicatorLabel) _indicatorLabel.text = Steps[_appliedIdx] + "%";
    }

    // ─── Построить команду ───────────────────────────────────────────────────
    private string BuildCmd(int idx)
    {
        int pct = Steps[idx];

        switch (Device)
        {
            case DeviceType.WaterPump:
                if (pct == 0) return "WATER_OFF";
                if (pct == 50) return "WATER_50";
                if (pct == 70) return "WATER_70";
                return "WATER_100";

            case DeviceType.ReagentPump:
                if (pct == 0) return "REAGENT_OFF";
                if (pct == 50) return "REAGENT_50";
                if (pct == 70) return "REAGENT_70";
                return "REAGENT_100";

            case DeviceType.Heater:
                return pct > 0 ? "HEATER_ON" : "HEATER_OFF";

            case DeviceType.DrainValve:
                return pct > 0 ? "VALVE_OPEN" : "VALVE_CLOSE";

            default: return "";
        }
    }

    // ─── Вспомогательные ─────────────────────────────────────────────────────
    private bool Blocked() => _busy || (GlobalModeControl.Instance?.IsAuto ?? true);

    private void PaintBtn(Button btn, bool active)
    {
        if (!btn) return;
        btn.interactable = active;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? colorBtnOn : colorBtnOff;
    }

    private static void NoTransition(Button btn)
    {
        if (btn) btn.transition = Selectable.Transition.None;
    }
}