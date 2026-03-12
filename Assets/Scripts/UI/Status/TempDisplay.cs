using UnityEngine;
using TMPro;

/// <summary>
/// Отображает температуру с датчика Arduino.
/// Читает ArduinoController_Connect.Instance.CurrentTempC в Update().
/// </summary>
public class TempDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _tempText;

    [Header("Формат")]
    public string numberFormat = "F1";
    public string unit = " °C";

    [Header("Цвета")]
    public Color colorNormal  = new Color(0.2f,  0.85f, 0.3f);
    public Color colorHot     = new Color(0.90f, 0.20f, 0.2f);
    public Color colorCold    = new Color(0.3f,  0.6f,  1.0f);

    [Header("Пороги")]
    public float tempMin = 20f;
    public float tempMax = 30f;

    void Start()
    {
        if (_tempText == null)
            _tempText = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Update()
    {
        if (_tempText == null) return;
        if (ArduinoController_Connect.Instance == null) return;

        float temp = ArduinoController_Connect.Instance.CurrentTempC;

        _tempText.text = temp.ToString(numberFormat) + unit;

        if (temp < tempMin)
            _tempText.color = colorCold;
        else if (temp > tempMax)
            _tempText.color = colorHot;
        else
            _tempText.color = colorNormal;
    }
}
