using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Вешается на любой UI-элемент.
/// При наведении показывает подсказку, при уходе — очищает текст.
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Текст подсказки")]
    [Tooltip("Заголовок окна (можно оставить пустым)")]
    public string Title = "";

    [TextArea(2, 6)]
    public string Body = "";

    [Header("Задержка показа (сек)")]
    [SerializeField] private float ShowDelay = 0.3f;

    private float _hoverTimer = 0f;
    private bool _hovering = false;
    private bool _shown = false;

    void Update()
    {
        if (!_hovering || _shown) return;

        _hoverTimer += Time.deltaTime;
        if (_hoverTimer >= ShowDelay)
        {
            _shown = true;
            TooltipSystem.Show(Title, Body);
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
}