using UnityEngine;
using TMPro;

/// <summary>
/// Управляет шириной (Scale X) объекта Water у насоса
/// и выводит процент в TextMeshPro.
/// Привязывается к GameObject "Water" (Pump → Blank → Water).
/// </summary>
public class PumpPrecent : MonoBehaviour
{
    [Header("Зависимости")]
    [Tooltip("Модель флотационного бака — источник u_PumpIn")]
    [SerializeField] private FlotationPlantModel _model;

    [Tooltip("TextMeshPro с процентами (Text (TMP) рядом с Water)")]
    [SerializeField] private TextMeshProUGUI _label;

    [Header("Настройки")]
    [Tooltip("Плавность изменения (0 = мгновенно, выше = плавнее)")]
    [Range(0f, 20f)]
    public float smoothSpeed = 8f;

    private float _displayValue = 0f;

    void Start()
    {
        if (_model == null)
        {
            _model = FindObjectOfType<FlotationPlantModel>();
            if (_model == null)
                Debug.LogError("[PumpWaterFill] FlotationPlantModel не найден!");
        }

      

        if (_label != null)
            _label.text = "0%";
    }

    void Update()
    {
        if (_model == null) return;

        float target = _model.u_PumpIn; // 0..1

        if (smoothSpeed > 0f)
            _displayValue = Mathf.Lerp(_displayValue, target, Time.deltaTime * smoothSpeed);
        else
            _displayValue = target;

      

        // Текст процентов
        if (_label != null)
            _label.text = Mathf.RoundToInt(_displayValue * 100f) + "%";
    }
}