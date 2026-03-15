using UnityEngine;

/// <summary>
/// Наполняет бак визуально — меняет Scale Y объекта Water
/// пропорционально уровню воды с датчика Arduino.
/// Читает ArduinoController_Connect.Instance.CurrentLevelCm напрямую в Update().
/// Pivot у Water должен быть внизу (Y = 0).
/// </summary>
public class AquariumWaterFill : MonoBehaviour
{
    [Header("Зависимости")]
    [Tooltip("Модель флотационного бака (только для цвета воды по Bio)")]
    [SerializeField] private FlotationPlantModel _model;

    [Header("Настройки уровня")]
    [Tooltip("Максимальный физический уровень датчика в см (calCm[4] на Arduino = 4.0)")]
    public float MaxLevelCm = 12.0f;

    [Tooltip("Плавность заполнения (0 = мгновенно)")]
    [Range(0f, 20f)]
    public float smoothSpeed = 5f;

    [Header("Цвета воды")]
    public Color colorClean = new Color(0.2f, 0.55f, 1f, 0.85f);
    public Color colorDirty = new Color(0.45f, 0.35f, 0.1f, 0.9f);

    [Tooltip("Максимальное биозагрязнение для нормировки цвета")]
    public float maxBio = 150f;

    // ── Приватные ─────────────────────────────────────────────────────────
    private UnityEngine.UI.Image _image;
    private float _displayFill = 0f;

    // =========================================================================
    void Start()
    {
        _image = GetComponent<UnityEngine.UI.Image>();

        transform.localScale = new Vector3(
            transform.localScale.x, 0f, transform.localScale.z);
    }

    void Update()
    {
        
        float levelCm = 0f;
        if (ArduinoController_Connect.Instance != null)
            levelCm = ArduinoController_Connect.Instance.CurrentLevelCm;

        float target = Mathf.Clamp(levelCm, 0f, MaxLevelCm) / MaxLevelCm;

        _displayFill = smoothSpeed > 0f
            ? Mathf.Lerp(_displayFill, target, Time.deltaTime * smoothSpeed)
            : target;

        transform.localScale = new Vector3(
            transform.localScale.x,
            _displayFill,
            transform.localScale.z);

        // Цвет воды по уровню загрязнения
        if (_image != null && _model != null)
        {
            float bioNorm = Mathf.Clamp01(_model.currentBioConcentration / maxBio);
            _image.color = Color.Lerp(colorClean, colorDirty, bioNorm);
        }
    }
}