using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Триггер для индикатора статуса подключения.
/// Текст обновляется в реальном времени пока курсор на индикаторе.
/// </summary>
public class ConnectionStatusTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float ShowDelay = 0.2f;

    private float _hoverTimer = 0f;
    private bool _hovering = false;
    private bool _shown = false;

    void Update()
    {
        if (!_hovering) return;

        if (!_shown)
        {
            _hoverTimer += Time.deltaTime;
            if (_hoverTimer >= ShowDelay)
            {
                _shown = true;
                ShowCurrentState();
            }
        }
        else
        {
            // Обновляем текст в реальном времени
            ShowCurrentState();
        }
    }

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

    private void ShowCurrentState()
    {
        if (ArduinoController_Connect.Instance == null)
        {
            TooltipSystem.Show("Статус подключения", "Контроллер не инициализирован.");
            return;
        }

        TooltipSystem.Show("Статус подключения",
            GetStateDescription(ArduinoController_Connect.Instance.CurrentState));
    }

    private string GetStateDescription(ArduinoController_Connect.ConnectionState state)
    {
        return state switch
        {
            ArduinoController_Connect.ConnectionState.Idle =>
                "Не подключено\nУстройство не обнаружено.",

            ArduinoController_Connect.ConnectionState.Scanning =>
                "Сканирование...\nПоиск доступных COM-портов.",

            ArduinoController_Connect.ConnectionState.Connecting =>
                "Подключение...\nУстановка соединения с портом.",

            ArduinoController_Connect.ConnectionState.Initializing =>
                "Инициализация...\nОжидание ответа от устройства.",

            ArduinoController_Connect.ConnectionState.Connected =>
                "Подключено\nУстройство успешно обнаружено и отвечает.",

            ArduinoController_Connect.ConnectionState.Lost =>
                "Соединение потеряно\nУстройство перестало отвечать.",

            _ => "Неизвестное состояние."
        };
    }
}