using UnityEngine;
using TMPro;

/// <summary>
/// Читает уровень напрямую из ArduinoController_Connect.Instance.CurrentLevelCm
/// в Update() — точно так же как AquariumWaterFill.
/// </summary>
public class LevelDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("Формат")]
    public string numberFormat = "F1";
    public string unit = " см";

    [Header("Цвета статуса")]
    public Color colorOk = new Color(0.2f, 0.85f, 0.3f);
    public Color colorWarning = new Color(0.95f, 0.80f, 0.1f);
    public Color colorError = new Color(0.90f, 0.20f, 0.2f);

    void Start()
    {
        if (_levelText == null)
            _levelText = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Update()
    {
        if (_levelText == null) return;
        if (ArduinoController_Connect.Instance == null) return;

        float level = ArduinoController_Connect.Instance.CurrentLevelCm;
        string status = ArduinoController_Connect.Instance.CurrentStatus;

        _levelText.text = level.ToString(numberFormat) + unit;

        if (status.Contains("OK"))
            _levelText.color = colorOk;
        else if (status.Contains("LOW") || status.Contains("OVER"))
            _levelText.color = colorError;
        else
            _levelText.color = colorWarning;
    }
}