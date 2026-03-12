using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вешается на кнопку/объект датчика.
/// По клику открывает указанную панель.
/// </summary>
public class OpenPanelOnClick : MonoBehaviour
{
    [Tooltip("Панель которую нужно открыть")]
    [SerializeField] private GameObject _panel;

    void Start()
    {
        // Если на объекте есть Button — подписываемся на него
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(Open);
    }

    public void Open()
    {
        if (_panel != null)
            _panel.SetActive(true);
    }
}