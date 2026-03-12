using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Вешается на любой UI-элемент.
/// При наведении показывает подсказку с актуальными параметрами датчиков.
/// Если _model назначен — Body заполняется автоматически из FlotationPlantModel.
/// </summary>
public class TriggerBak : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Текст подсказки")]
    [Tooltip("Заголовок окна (можно оставить пустым)")]
    public string Title = "";

    [TextArea(2, 6)]
    [Tooltip("Статический текст. Если Model назначена — заменяется параметрами датчиков.")]
    public string Body = "";

    [Header("Параметры датчиков (опционально)")]
    [Tooltip("Если назначена — Body заполняется автоматически актуальными данными.")]
    [SerializeField] private FlotationPlantModel _model;

    [Tooltip("Показывать температуру")]
    public bool showTemperature = true;
    [Tooltip("Показывать уровень (см)")]
    public bool showLevel = true;
    [Tooltip("Показывать биозагрязнение")]
    public bool showBio = true;
    [Tooltip("Показывать концентрацию флокулянта")]
    public bool showFlocculant = true;
    [Tooltip("Показывать уставки рядом с текущим значением")]
    public bool showSetpoints = true;

    [Header("Задержка показа (сек)")]
    [SerializeField] private float ShowDelay = 0.3f;

    // ─── Приватные ────────────────────────────────────────────
    private float _hoverTimer = 0f;
    private bool _hovering = false;
    private bool _shown = false;

    // =========================================================================
    void Start()
    {
        if (_model == null)
            _model = FindObjectOfType<FlotationPlantModel>();
    }

    void Update()
    {
        if (!_hovering || _shown) return;

        _hoverTimer += Time.deltaTime;
        if (_hoverTimer >= ShowDelay)
        {
            _shown = true;
            TooltipSystem.Show(Title, BuildBody());
        }
    }

    // =========================================================================
    //  Построить текст подсказки из параметров модели
    // =========================================================================
    string BuildBody()
    {
        if (_model == null) return Body;

        var sb = new System.Text.StringBuilder();

        // ── Температура ───────────────────────────────────────
        if (showTemperature)
        {
            sb.Append("Температура: ");
            sb.Append(_model.currentTemperature.ToString("F1"));
            sb.Append("°C");
            if (showSetpoints)
            {
                sb.Append("  /  уставка: ");
                sb.Append(_model.setpointTemperature.ToString("F1"));
                sb.Append("°C");
            }
            sb.AppendLine();
        }

        // ── Уровень (объём → см, 5 дм³ = 4 см) ───────────────
        if (showLevel)
        {
            float cm = (_model.currentVolume / FlotationPlantModel.MaxVolume) * 4f;
            float spCm = (_model.setpointVolume / FlotationPlantModel.MaxVolume) * 4f;
            sb.Append("Уровень: ");
            sb.Append(cm.ToString("F1"));
            sb.Append(" см");
            if (showSetpoints)
            {
                sb.Append("  /  уставка: ");
                sb.Append(spCm.ToString("F1"));
                sb.Append(" см");
            }
            sb.AppendLine();
        }

        // ── Биозагрязнение ────────────────────────────────────
        if (showBio)
        {
            sb.Append("Биозагрязнение: ");
            sb.Append(_model.currentBioConcentration.ToString("F1"));
            sb.Append(" у.е.");
            if (showSetpoints)
            {
                sb.Append("/  уставка: ");
                sb.Append(_model.setpointBio.ToString("F1"));
                sb.Append(" у.е.");
            }
            sb.AppendLine();
        }

        // ── Флокулянт ─────────────────────────────────────────
        if (showFlocculant)
        {
            sb.Append("Флокулянт: ");
            sb.Append(_model.currentFlocculantConc.ToString("F1"));
            sb.AppendLine(" мг/л");
        }

        // Дополнительный статический текст (если есть)
        if (!string.IsNullOrEmpty(Body))
        {
            sb.Append(Body);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // =========================================================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        _hoverTimer = 0f;
        _shown = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
        _shown = false;
        _hoverTimer = 0f;
        TooltipSystem.Hide();
    }

    void OnDisable()
    {
        _hovering = false;
        _shown = false;
        TooltipSystem.Hide();
    }
}