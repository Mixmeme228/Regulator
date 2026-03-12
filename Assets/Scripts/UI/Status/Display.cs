using UnityEngine;
using TMPro;

/// <summary>
/// Отображает показания датчиков флотационного бака в TextMeshPro.
/// Температура, уровень воды, биозагрязнение, концентрация флокулянта.
/// </summary>
public class SensorDisplay : MonoBehaviour
{
    [Header("Зависимости")]
    [SerializeField] private FlotationPlantModel _model;

    [Header("Текстовые поля")]
    [Tooltip("Поле температуры — формат: 24.5°")]
    [SerializeField] private TextMeshProUGUI _labelTemperature;

    [Tooltip("Поле уровня воды — формат: 2.4 см")]
    [SerializeField] private TextMeshProUGUI _labelLevel;

    [Tooltip("Поле биозагрязнения — формат: 87.3 у.е.")]
    [SerializeField] private TextMeshProUGUI _labelBio;

    [Tooltip("Поле концентрации флокулянта — формат: 12.4 мг/л")]
    [SerializeField] private TextMeshProUGUI _labelFlocculant;

    [Header("Цвета")]
    [Tooltip("Цвет значения в норме")]
    public Color colorNormal = Color.white;

    [Tooltip("Цвет при отклонении от уставки")]
    public Color colorWarning = new Color(1f, 0.4f, 0.1f, 1f);   // оранжевый

    [Tooltip("Аварийный цвет (критическое отклонение)")]
    public Color colorDanger = new Color(1f, 0.05f, 0.05f, 1f); // красный

    [Header("Пороги")]
    [Tooltip("Допустимое отклонение температуры, °C")]
    public float tempTolerance = 2f;

    [Tooltip("Допустимое отклонение уровня, см")]
    public float levelTolerance = 0.3f;

    [Tooltip("Аварийный порог уровня, см (макс 4 см = 5 л)")]
    public float levelDangerThreshold = 3.7f;

    [Tooltip("Допустимое превышение биозагрязнения над уставкой, у.е.")]
    public float bioTolerance = 5f;

    [Tooltip("Минимальная концентрация флокулянта для нормы, мг/л\n" +
             "Ниже этого значения — предупреждение (флокулянт заканчивается)")]
    public float flocMinNormal = 5f;

    // =========================================================================
    void Start()
    {
        if (_model == null)
        {
            _model = FindObjectOfType<FlotationPlantModel>();
            if (_model == null)
                Debug.LogError("[SensorDisplay] FlotationPlantModel не найден!");
        }

        UpdateLabels();
    }

    void Update()
    {
        if (_model == null) return;
        UpdateLabels();
    }

    // =========================================================================
    void UpdateLabels()
    {
        // ── Температура ───────────────────────────────────────
        if (_labelTemperature != null)
        {
            float t = _model.currentTemperature;
            _labelTemperature.text = t.ToString("F1") + "°";
            _labelTemperature.color = Mathf.Abs(t - _model.setpointTemperature) > tempTolerance
                ? colorWarning : colorNormal;
        }

        // ── Уровень (объём → см, 5 дм³ = 4 см) ───────────────
        if (_labelLevel != null)
        {
            float v = _model.currentVolume;
            float cm = (v / FlotationPlantModel.MaxVolume) * 4f;
            float spCm = (_model.setpointVolume / FlotationPlantModel.MaxVolume) * 4f;

            _labelLevel.text = cm.ToString("F1") + " см";
            _labelLevel.color = cm >= levelDangerThreshold
                ? colorDanger
                : Mathf.Abs(cm - spCm) > levelTolerance
                    ? colorWarning : colorNormal;
        }

        // ── Биозагрязнение ────────────────────────────────────
        if (_labelBio != null)
        {
            float b = _model.currentBioConcentration;
            _labelBio.text = b.ToString("F1") + " у.е.";
            _labelBio.color = (b - _model.setpointBio) > bioTolerance
                ? colorWarning : colorNormal;
        }

        // ── Концентрация флокулянта ───────────────────────────
        if (_labelFlocculant != null)
        {
            float cf = _model.currentFlocculantConc;
            _labelFlocculant.text = cf.ToString("F1") + " мг/л";
            _labelFlocculant.color = cf < flocMinNormal
                ? colorWarning : colorNormal;
        }
    }
}