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
    [SerializeField] private Button _btnStop;   // Стоп — обнулить, панель остаётся
    [SerializeField] private Button _btnClose;  // × — только скрыть панель

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

    // ─── Шаги ────────────────────────────────────────────────────────────────
    private static readonly int[] StepsWater = { 0, 50, 70, 100 };
    private static readonly int[] StepsReagent = { 0, 25, 50, 70, 100 };
    private static readonly int[] StepsBinary = { 0, 100 };

    private int[] Steps
    {
        get
        {
            switch (Device)
            {
                case DeviceType.WaterPump: return StepsWater;
                case DeviceType.ReagentPump: return StepsReagent;
                default: return StepsBinary;
            }
        }
    }

    private string KeyWant => $"MDC_{Device}_wantIdx";
    private string KeyApplied => $"MDC_{Device}_appliedIdx";

    private int _wantIdx = 0;
    private int _appliedIdx = 0;
    private bool _busy = false;
    private bool _prevAuto = true;

  
    private bool IsReagent => Device == DeviceType.ReagentPump;

    // =========================================================================
    void Awake()
    {
        NoTransition(_btnMinus);
        NoTransition(_btnPlus);
        NoTransition(_btnSet);
        NoTransition(_btnStop);
    }

    void Start()
    {
        LoadState();

        _btnMinus?.onClick.AddListener(ClickMinus);
        _btnPlus?.onClick.AddListener(ClickPlus);
        _btnSet?.onClick.AddListener(ClickSet);
        _btnStop?.onClick.AddListener(ClickStop);
        _btnClose?.onClick.AddListener(ClickClose);

        _prevAuto = GlobalModeControl.Instance?.IsAuto ?? true;
        Redraw();
    }

    void Update()
    {
        bool g = GlobalModeControl.Instance?.IsAuto ?? true;
        if (g == _prevAuto) return;
        _prevAuto = g;
        // Реагент не сбрасывает wantIdx при переключении режима
        if (!g && !IsReagent) _wantIdx = _appliedIdx;
        Redraw();
    }

    // ─── Сохранение / Загрузка ───────────────────────────────────────────────
    private void SaveState()
    {
        PlayerPrefs.SetInt(KeyWant, _wantIdx);
        PlayerPrefs.SetInt(KeyApplied, _appliedIdx);
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        int maxIdx = Steps.Length - 1;
        _wantIdx = Mathf.Clamp(PlayerPrefs.GetInt(KeyWant, 0), 0, maxIdx);
        _appliedIdx = Mathf.Clamp(PlayerPrefs.GetInt(KeyApplied, 0), 0, maxIdx);
    }

    // ─── Кнопки ──────────────────────────────────────────────────────────────
    private void ClickMinus()
    {
        if (Blocked() || _wantIdx == 0) return;
        _wantIdx--;
        SaveState();
        Redraw();
    }

    private void ClickPlus()
    {
        if (Blocked() || _wantIdx >= Steps.Length - 1) return;
        _wantIdx++;
        SaveState();
        Redraw();
    }

    private void ClickSet()
    {
        if (Blocked() || _wantIdx == _appliedIdx) return;
        SendCmd(_wantIdx, ok =>
        {
            if (ok) _appliedIdx = _wantIdx;
            else _wantIdx = _appliedIdx;
            SaveState();
        });
    }

    private void ClickStop()
    {
        if (Blocked()) return;
        if (_appliedIdx == 0 && _wantIdx == 0) return;

        _wantIdx = 0;
        SendCmd(0, ok =>
        {
            if (ok) _appliedIdx = 0;
            else _wantIdx = _appliedIdx;
            SaveState();
        });
    }

    private void ClickClose()
    {
        if (_busy) return;
        gameObject.SetActive(false);
    }

    // ─── Отправка команды ────────────────────────────────────────────────────
    private void SendCmd(int idx, System.Action<bool> onResult)
    {
        var ard = ArduinoController_Connect.Instance;
        if (ard == null || !ard.isConnected) return;

        _busy = true;
        Redraw();

        ard.SendCommand(BuildCmd(idx), ok =>
        {
            onResult(ok);
            _busy = false;
            Redraw();
        });
    }

    // ─── Публичный метод для CloseAllPanels ────────────────────────────────
    public void ClosePanel()
    {
        if (_busy) { StartCoroutine(CloseWhenFree()); return; }
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
        if ((GlobalModeControl.Instance?.IsAuto ?? true) && !IsReagent)
            _wantIdx = _appliedIdx;
        SaveState();
        Redraw();
    }

    // ─── Отрисовка ───────────────────────────────────────────────────────────
    private void Redraw()
    {
        bool isAuto = GlobalModeControl.Instance?.IsAuto ?? true;
        bool can = !_busy && (!isAuto || IsReagent); // реагент всегда can
        bool isPump = Device == DeviceType.WaterPump || Device == DeviceType.ReagentPump;
        bool isBinary = !isPump;

        PaintBtn(_btnMinus, can && _wantIdx > 0);
        PaintBtn(_btnPlus, can && _wantIdx < Steps.Length - 1);
        PaintBtn(_btnSet, can && _wantIdx != _appliedIdx);

        if (_btnStop)
        {
            _btnStop.gameObject.SetActive(isPump);
            PaintBtn(_btnStop, can && isPump && _appliedIdx > 0);
        }

        int displayPct = isAuto && !IsReagent ? Steps[_appliedIdx] : Steps[_wantIdx];

        if (_labelValue)
            _labelValue.text = isBinary
                ? (displayPct > 0 ? "Вкл" : "Выкл")
                : displayPct + "%";

        bool on = _appliedIdx > 0;
        if (_indicatorBg) _indicatorBg.color = on ? colorOn : colorOff;
        if (_indicatorLabel)
            _indicatorLabel.text = isBinary
                ? (on ? "Вкл" : "Выкл")
                : Steps[_appliedIdx] + "%";
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
                if (pct == 25) return "REAGENT_25";
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
    
    private bool Blocked() => _busy || (!IsReagent && (GlobalModeControl.Instance?.IsAuto ?? true));

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