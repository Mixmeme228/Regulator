using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CommandInput : MonoBehaviour
{
    [Header("Поле ввода")]
    [SerializeField] private TMP_InputField _inputField;

    [Header("Кнопка отправки (необязательно)")]
    [SerializeField] private Button _btnSend;

    [Header("Цвета плейсхолдера")]
    public Color colorIdle = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color colorWaiting = new Color(0.9f, 0.7f, 0.1f, 1f);
    public Color colorOk = new Color(0.1f, 0.8f, 0.2f, 1f);
    public Color colorError = new Color(0.9f, 0.2f, 0.2f, 1f);

    private bool _busy = false;

    void Start()
    {
        _btnSend?.onClick.AddListener(TrySend);

        if (_inputField != null)
        {
            _inputField.onSubmit.AddListener(_ => TrySend());
            SetPlaceholder("Введите команду...", colorIdle);
        }
    }

    private void TrySend()
    {
        if (_inputField == null || _busy) return;

        string cmd = _inputField.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(cmd)) return;

        var arduino = ArduinoController_Connect.Instance;
        if (arduino == null || !arduino.isConnected)
        {
            SetPlaceholder("Arduino не подключён!", colorError);
            _inputField.text = "";
            return;
        }

        _busy = true;
        _inputField.interactable = false;
        if (_btnSend) _btnSend.interactable = false;

        SetPlaceholder("Отправка: " + cmd + "...", colorWaiting);
        _inputField.text = "";

        arduino.SendCommand(cmd, ok =>
        {
            _busy = false;
            _inputField.interactable = true;
            if (_btnSend) _btnSend.interactable = true;

            // Только ASCII — без ✓ ✗
            SetPlaceholder(ok ? "OK: " + cmd : "ERROR: " + cmd, ok ? colorOk : colorError);

            Invoke(nameof(ResetPlaceholder), 2f);
            _inputField.ActivateInputField();
        });
    }

    private void ResetPlaceholder() =>
        SetPlaceholder("Введите команду и нажмите Enter...", colorIdle);

    private void SetPlaceholder(string text, Color color)
    {
        if (_inputField == null) return;
        var ph = _inputField.placeholder as TextMeshProUGUI;
        if (ph == null) return;
        ph.text = text;
        ph.color = color;
    }
}