using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Управляет полоской нагревателя (Scale X) + процентами в TMP.
/// Также окрашивает иконку нагревателя от белого → красного.
/// Привязывается к GameObject "Water" (Heater → Status → Blank → Water).
/// </summary>
public class HeaterFill : MonoBehaviour
{
    [Header("Зависимости")]
    [Tooltip("Модель флотационного бака — источник u_Heater")]
    [SerializeField] private FlotationPlantModel _model;

    

    [Tooltip("Image иконки нагревателя — будет краснеть при работе")]
    [SerializeField] private Image _heaterIcon;

    [Header("Настройки")]
    [Tooltip("Плавность изменения (0 = мгновенно, выше = плавнее)")]
    [Range(0f, 20f)]
    public float smoothSpeed = 8f;

    [Header("Цвета иконки нагревателя")]
    [Tooltip("Цвет иконки когда нагреватель выключен")]
    public Color iconColorOff = Color.white;

    [Tooltip("Цвет иконки на полной мощности")]
    public Color iconColorOn = new Color(1f, 0.15f, 0f, 1f); // ярко-красный

    private float _displayValue = 0f;

    void Start()
    {
        if (_model == null)
        {
            _model = FindObjectOfType<FlotationPlantModel>();
            if (_model == null)
                Debug.LogError("[HeaterFill] FlotationPlantModel не найден!");
        }

       

       

        if (_heaterIcon != null)
            _heaterIcon.color = iconColorOff;
    }

    void Update()
    {
        if (_model == null) return;

        float target = _model.u_Heater; // 0..1

        if (smoothSpeed > 0f)
            _displayValue = Mathf.Lerp(_displayValue, target, Time.deltaTime * smoothSpeed);
        else
            _displayValue = target;


        // ── Цвет иконки нагревателя ───────────────────────────
        if (_heaterIcon != null)
            _heaterIcon.color = Color.Lerp(iconColorOff, iconColorOn, _displayValue);
    }
}