using UnityEngine;

/// <summary>
/// Управляет шириной (Scale X) объекта Water у насоса.
/// Привязывается к GameObject "Water" (Pump → Blank → Water).
/// Читает u_PumpIn из FlotationPlantModel и масштабирует по X.
/// </summary>
public class HeaterFillStatus : MonoBehaviour
{
    [Header("Зависимости")]
    [Tooltip("Модель флотационного бака — источник u_PumpIn")]
    [SerializeField] private FlotationPlantModel _model;

    [Header("Настройки")]
    [Tooltip("Плавность изменения (0 = мгновенно, выше = плавнее)")]
    [Range(0f, 20f)]
    public float smoothSpeed = 8f;

    private float _displayValue = 0f;

    // =========================================================================
    void Start()
    {
        if (_model == null)
        {
            _model = FindObjectOfType<FlotationPlantModel>();
            if (_model == null)
                Debug.LogError("[PumpWaterFill] FlotationPlantModel не найден! Назначь вручную.");
        }

        // Начальный scale X = 0
        transform.localScale = new Vector3(0f, transform.localScale.y, transform.localScale.z);
    }

    void Update()
    {
        if (_model == null) return;

        float target = _model.u_Heater; // 0..1

        // Плавное сглаживание
        if (smoothSpeed > 0f)
            _displayValue = Mathf.Lerp(_displayValue, target, Time.deltaTime * smoothSpeed);
        else
            _displayValue = target;

        // Меняем только Scale X, Y и Z не трогаем
        transform.localScale = new Vector3(
            _displayValue,
            transform.localScale.y,
            transform.localScale.z);
    }
}