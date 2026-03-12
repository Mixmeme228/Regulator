using UnityEngine;
using TMPro;

/// <summary>
/// Синглтон-менеджер тултипов.
/// Окно всегда видимо и стоит на месте — не следует за курсором.
/// При отсутствии наведения текст пустой.
/// </summary>
public class TooltipSystem : MonoBehaviour
{
    public static TooltipSystem Instance { get; private set; }

    [Header("Окно подсказки")]
    [SerializeField] private GameObject TooltipPanel;
    [SerializeField] private TMP_Text TitleText;
    [SerializeField] private TMP_Text BodyText;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;

        // Окно всегда активно
        TooltipPanel.SetActive(true);
        ClearText();
    }

    // ─── Публичные методы ────────────────────────────────────────────────────
    public static void Show(string title, string body)
    {
        if (Instance == null) return;
        Instance.ShowInternal(title, body);
    }

    public static void Show(string body)
    {
        if (Instance == null) return;
        Instance.ShowInternal("", body);
    }

    public static void Hide()
    {
        if (Instance == null) return;
        Instance.ClearText();
    }

    // ─── Внутренние методы ───────────────────────────────────────────────────
    private void ShowInternal(string title, string body)
    {
        if (TitleText != null)
        {
            TitleText.text = title;
            TitleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }

        if (BodyText != null)
            BodyText.text = body;
    }

    private void ClearText()
    {
        if (TitleText != null)
        {
            TitleText.text = "";
            TitleText.gameObject.SetActive(false);
        }

        if (BodyText != null)
            BodyText.text = "";
    }
}