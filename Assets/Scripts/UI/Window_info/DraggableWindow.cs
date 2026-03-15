using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Окно: иконка открывает, кнопка × закрывает, заголовок — тащить.
/// </summary>
public class DraggableWindow : MonoBehaviour,
    IPointerDownHandler, IDragHandler
{
    [Header("Кнопки управления")]
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;

    [Header("Перетаскивание")]
    [Tooltip("Заголовок окна — за него тащим. Если не задан — тащится за всё окно.")]
    [SerializeField] private RectTransform _dragHandle;

    [Header("Настройки")]
    public bool openOnStart = false;
    public bool animateScale = true;
    [Range(5f, 30f)]
    public float animSpeed = 15f;

    [Header("Масштаб окна")]
    [Tooltip("Итоговый масштаб окна когда оно открыто (1 = оригинал, 2 = в два раза больше)")]
    [Range(0.1f, 5f)]
    public float windowScale = 1f;

    // ─── Приватные ────────────────────────────────────────────
    private RectTransform _rect;
    private Canvas _canvas;
    private float _targetScale = 0f;
    private Vector2 _dragOffset;
    private bool _dragging;

    // =========================================================================
    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        _openButton?.onClick.AddListener(Open);
        _closeButton?.onClick.AddListener(Close);

        _targetScale = openOnStart ? windowScale : 0f;
        transform.localScale = Vector3.one * _targetScale;
        gameObject.SetActive(openOnStart);
    }

    void Update()
    {
        if (!animateScale) return;
        float cur = transform.localScale.x;
        float target = _targetScale;
        if (Mathf.Approximately(cur, target)) return;
        float next = Mathf.MoveTowards(cur, target, Time.deltaTime * animSpeed);
        transform.localScale = Vector3.one * next;
        if (_targetScale <= 0f && next <= 0.01f)
        {
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }
    }

    // ── Открыть / Закрыть ────────────────────────────────────
    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _targetScale = windowScale;
        if (!animateScale) transform.localScale = Vector3.one * windowScale;
    }

    public void Close()
    {
        _targetScale = 0f;
        if (!animateScale) gameObject.SetActive(false);
    }

    // ── Изменить масштаб на лету ──────────────────────────────
    /// <summary>Изменить масштаб окна в рантайме.</summary>
    public void SetWindowScale(float scale)
    {
        windowScale = Mathf.Clamp(scale, 0.1f, 5f);
        if (gameObject.activeSelf)
        {
            _targetScale = windowScale;
            if (!animateScale) transform.localScale = Vector3.one * windowScale;
        }
    }

    // ── Перетаскивание ────────────────────────────────────────
    public void OnPointerDown(PointerEventData e)
    {
        if (_dragHandle != null)
            _dragging = RectTransformUtility.RectangleContainsScreenPoint(
                _dragHandle, e.position, e.pressEventCamera);
        else
            _dragging = true;

        if (!_dragging) return;
        transform.SetAsLastSibling();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            e.position, e.pressEventCamera, out Vector2 lp);
        _dragOffset = (Vector2)_rect.localPosition - lp;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            e.position, e.pressEventCamera, out Vector2 lp);

        Vector2 pos = lp + _dragOffset;

        // Ограничить в пределах Canvas
        var cr = _canvas.GetComponent<RectTransform>();
        var half = cr.sizeDelta * 0.5f;
        var hw = _rect.sizeDelta * _rect.localScale.x * 0.5f; // учитываем scale
        pos.x = Mathf.Clamp(pos.x, -half.x + hw.x, half.x - hw.x);
        pos.y = Mathf.Clamp(pos.y, -half.y + hw.y, half.y - hw.y);

        _rect.localPosition = pos;
    }
}