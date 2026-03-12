using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlantStatusTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Пороги")]
    public float alarmLevelCm = 3.7f;
    public float warnTempDelta = 3.0f;
    public float warnLevelDelta = 0.5f;

    [Header("UI")]
    [SerializeField] private RawImage _icon;
    [SerializeField] private FlotationPlantModel _model;
    [SerializeField] private float ShowDelay = 0.2f;

    public Color colorOk = new Color(0.1f, 0.80f, 0.2f, 1f);
    public Color colorWarning = new Color(0.95f, 0.75f, 0.05f, 1f);
    public Color colorAlarm = new Color(0.90f, 0.10f, 0.10f, 1f);

    private bool _hovering, _shown;
    private float _hoverTimer, _iconTimer;

    void Start()
    {
        if (_model == null) _model = FindObjectOfType<FlotationPlantModel>();
        RefreshIcon();
    }

    void Update()
    {
        _iconTimer += Time.deltaTime;
        if (_iconTimer >= 1f) { _iconTimer = 0f; RefreshIcon(); }

        if (!_hovering) return;
        _hoverTimer += Time.deltaTime;
        if (!_shown && _hoverTimer >= ShowDelay) { _shown = true; _hoverTimer = 0f; ShowTooltip(); }
        else if (_shown && _hoverTimer >= 0.5f) { _hoverTimer = 0f; ShowTooltip(); }
    }

    public void OnPointerEnter(PointerEventData e) { _hovering = true; _hoverTimer = 0f; _shown = false; }
    public void OnPointerExit(PointerEventData e) { _hovering = false; _shown = false; TooltipSystem.Hide(); }
    void OnDisable() { _hovering = false; _shown = false; TooltipSystem.Hide(); }

    private float LevelCm(float vol) => vol * 1000f / (35f * 35f);

    private void RefreshIcon()
    {
        Evaluate(out int lvl, out _);
        if (_icon) _icon.color = lvl == 2 ? colorAlarm : lvl == 1 ? colorWarning : colorOk;
    }

    private void ShowTooltip()
    {
        Evaluate(out int lvl, out string info);
        string title = lvl == 2 ? "АВАРИЯ" : lvl == 1 ? "Предупреждение" : "Норма";
        TooltipSystem.Show(title, info);
    }

    private void Evaluate(out int level, out string info)
    {
        if (_model == null) { level = 1; info = "Модель не найдена."; return; }

        float lv = LevelCm(_model.currentVolume);
        float lsp = LevelCm(_model.setpointVolume);
        float td = Mathf.Abs(_model.currentTemperature - _model.setpointTemperature);
        float ld = Mathf.Abs(lv - lsp);

        level = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Ур: {lv:F2}/{lsp:F2} см");
        sb.AppendLine($"Т:  {_model.currentTemperature:F1}/{_model.setpointTemperature:F1} °C");

        if (lv >= alarmLevelCm)
        {
            sb.Append($"АВАРИЯ: уровень {lv:F2} см");
            level = 2;
        }
        else
        {
            if (td > warnTempDelta) { sb.AppendLine($"Темп отклонение: +{td:F1}°C"); level = 1; }
            if (ld > warnLevelDelta) { sb.AppendLine($"Уровень отклонение: +{ld:F2} см"); level = Mathf.Max(level, 1); }
            if (level == 0) sb.Append("Норма");
        }

        info = sb.ToString();
    }
}