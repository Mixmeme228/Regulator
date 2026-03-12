using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SetpointPanel : MonoBehaviour
{
    public enum SetpointType { Temperature, Volume, Bio }

    private const float TANK_AREA_CM2 = 35f * 35f;
    private const float MAX_LEVEL_CM = 4.0f;
    private const float MIN_LEVEL_CM = 0.1f;

    [Header("Тип уставки")]
    public SetpointType Type = SetpointType.Temperature;

    [Header("UI")]
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private Button _btnSet;
    [SerializeField] private Button _btnClose;

    [Header("Ссылки")]
    [SerializeField] private FlotationPlantModel _model;

    [Header("Цвета плейсхолдера")]
    public Color colorIdle = new Color(0.4f, 0.4f, 0.4f, 1f);
    public Color colorOk = new Color(0.1f, 0.8f, 0.2f, 1f);
    public Color colorError = new Color(0.9f, 0.2f, 0.2f, 1f);

    // =========================================================================
    void Start()
    {
        _btnSet?.onClick.AddListener(OnSet);
        _btnClose?.onClick.AddListener(OnClose);
        if (_input != null)
        {
            _input.contentType = TMP_InputField.ContentType.DecimalNumber;
            _input.onSubmit.AddListener(_ => OnSet());
        }
    }

    void OnEnable()
    {
        if (_input != null) _input.text = "";
        CancelInvoke();
        ShowCurrentValue();
        _input?.ActivateInputField();
    }

    // ── Применить уставку ────────────────────────────────────────────────────
    private void OnSet()
    {
        if (_input == null || _model == null) return;

        string raw = _input.text.Trim().Replace(',', '.');
        if (!float.TryParse(raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float value))
        {
            SetPlaceholder("Неверное число!", colorError);
            _input.text = "";
            Invoke(nameof(ShowCurrentValue), 1.5f);
            return;
        }

        var arduino = ArduinoController_Connect.Instance;

        switch (Type)
        {
            case SetpointType.Temperature:
                value = Mathf.Clamp(value, 0f, 50f);
                _model.SetTargetTemperature(value);
                // SET_TEMP x
                arduino?.SendCommand(
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "SET_TEMP {0:F1}", value));
                break;

            case SetpointType.Volume:
                float levelCm = Mathf.Clamp(value, MIN_LEVEL_CM, MAX_LEVEL_CM);
                float volumeDm3 = LevelToVolume(levelCm);
                _model.SetTargetVolume(volumeDm3);
                // SET_LEVEL x (в см)
                arduino?.SendCommand(
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "SET_LEVEL {0:F2}", levelCm));
                break;

            case SetpointType.Bio:
                _model.SetTargetBio(value);
                // Bio — Arduino управляет реагентом сам через PI,
                // уставка передаётся через DATA BIO:x CF:x
                // (отправляется из другого места при следующем DATA-цикле)
                break;
        }

        _input.text = "";
        SetPlaceholder("OK: " + GetDisplayValue().ToString("F2") + GetUnit(), colorOk);
        Invoke(nameof(ShowCurrentValue), 1.5f);
    }

    private void OnClose()
    {
        CancelInvoke();
        if (_input != null) _input.text = "";
        gameObject.SetActive(false);
    }

    private void ShowCurrentValue()
    {
        if (_model == null) return;
        SetPlaceholder(GetDisplayValue().ToString("F2") + GetUnit(), colorIdle);
    }

    private float GetDisplayValue()
    {
        if (_model == null) return 0f;
        switch (Type)
        {
            case SetpointType.Temperature: return _model.setpointTemperature;
            case SetpointType.Volume: return VolumeToLevel(_model.setpointVolume);
            case SetpointType.Bio: return _model.setpointBio;
            default: return 0f;
        }
    }

    private string GetUnit()
    {
        switch (Type)
        {
            case SetpointType.Temperature: return " °C";
            case SetpointType.Volume: return " см";
            case SetpointType.Bio: return " у.е.";
            default: return "";
        }
    }

    // ── Конвертация ──────────────────────────────────────────────────────────
    private static float VolumeToLevel(float volumeDm3) => volumeDm3 * 1000f / TANK_AREA_CM2;
    private static float LevelToVolume(float levelCm) => levelCm * TANK_AREA_CM2 / 1000f;

    // ── UI ───────────────────────────────────────────────────────────────────
    private void SetPlaceholder(string text, Color color)
    {
        if (_input == null) return;
        var ph = _input.placeholder as TextMeshProUGUI;
        if (ph == null) return;
        ph.text = text;
        ph.color = color;
    }
}