using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вешается на кнопку «Закрыть всё».
/// При нажатии вызывает ClickClose() на всех активных ManualDeviceControl
/// и дополнительно деактивирует любые GameObject из списка _extraPanels.
/// </summary>
public class CloseAllPanels : MonoBehaviour
{
    [Header("Кнопка-триггер")]
    [SerializeField] private Button _closeAllButton;

    [Header("Дополнительные панели (необязательно)")]
    [Tooltip("Любые GameObject-окна, которые нужно скрыть помимо ManualDeviceControl")]
    [SerializeField] private GameObject[] _extraPanels;

    // ────────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (_closeAllButton != null)
            _closeAllButton.onClick.AddListener(CloseAll);
    }

    // ────────────────────────────────────────────────────────────────────────
    public void CloseAll()
    {
        // 1. Найти все ManualDeviceControl на сцене (включая неактивные объекты)
        ManualDeviceControl[] panels =
            FindObjectsByType<ManualDeviceControl>(FindObjectsInactive.Include,
                                                   FindObjectsSortMode.None);

        foreach (ManualDeviceControl panel in panels)
        {
            // Закрываем только те, чей GameObject сейчас активен
            if (panel.gameObject.activeInHierarchy)
            {
                panel.ClosePanel();
            }
        }

        // 2. Скрыть дополнительные панели из инспектора
        if (_extraPanels != null)
        {
            foreach (GameObject go in _extraPanels)
            {
                if (go != null && go.activeSelf)
                    go.SetActive(false);
            }
        }
    }
}