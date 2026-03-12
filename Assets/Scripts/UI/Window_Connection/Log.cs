using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LogPanel : MonoBehaviour
{
    // ─── Типы логов ──────────────────────────────────────────────────────────
    public enum LogType
    {
        Info,
        Warning,
        Error,
        Success,
        Debug,
    }

    // ─── Одна запись лога ────────────────────────────────────────────────────
    private class LogEntry
    {
        public LogType Type;
        public string Message;
        public string Timestamp;
        public GameObject GameObject;   // инстанс текстового объекта
    }

    // ─────────────────────────────────────────────────────────────────────────
    [Header("Контейнер")]
    [Tooltip("Content объект внутри ScrollView (с Vertical Layout Group)")]
    [SerializeField] private Transform LogContainer;

    [Tooltip("ScrollRect для авто-прокрутки вниз")]
    [SerializeField] private ScrollRect ScrollRect;

    [Header("Префаб строки")]
    [Tooltip("Префаб с компонентом TextMeshProUGUI (можно оставить пустым — создастся сам)")]
    [SerializeField] private GameObject LogLinePrefab;

    [Header("Фильтрация")]
    [SerializeField] private bool ShowInfo = true;
    [SerializeField] private bool ShowWarning = true;
    [SerializeField] private bool ShowError = true;
    [SerializeField] private bool ShowSuccess = true;
    [SerializeField] private bool ShowDebug = true;

    [Header("Кнопки-фильтры (опционально)")]
    [SerializeField] private Toggle ToggleInfo;
    [SerializeField] private Toggle ToggleWarning;
    [SerializeField] private Toggle ToggleError;
    [SerializeField] private Toggle ToggleSuccess;
    [SerializeField] private Toggle ToggleDebug;

    [Header("Настройки")]
    [SerializeField] private int MaxLogs = 64;
    [SerializeField] private float FontSize = 13f;
    [SerializeField] private bool ShowTimestamp = true;
    [SerializeField] private bool AutoScroll = true;

    // ─── Цвета по типу ───────────────────────────────────────────────────────
    private static readonly Dictionary<LogType, Color> LogColors = new()
    {
        { LogType.Info,    new Color(0.85f, 0.85f, 0.85f) },   // светло-серый
        { LogType.Warning, new Color(1.00f, 0.80f, 0.20f) },   // жёлтый
        { LogType.Error,   new Color(1.00f, 0.30f, 0.30f) },   // красный
        { LogType.Success, new Color(0.30f, 0.90f, 0.45f) },   // зелёный
        { LogType.Debug,   new Color(0.50f, 0.75f, 1.00f) },   // голубой
    };

    // ─── Префиксы по типу ────────────────────────────────────────────────────
    private static readonly Dictionary<LogType, string> LogPrefixes = new()
    {
        { LogType.Info,    "[INFO]    " },
        { LogType.Warning, "[WARN]    " },
        { LogType.Error,   "[ERROR]   " },
        { LogType.Success, "[OK]      " },
        { LogType.Debug,   "[DEBUG]   " },
    };

    

    private readonly LinkedList<LogEntry> _entries = new();

    // ─────────────────────────────────────────────────────────────────────────
    

    void Start()
    {
        // Подписываем тогглы если назначены
        if (ToggleInfo != null) ToggleInfo.onValueChanged.AddListener(v => { ShowInfo = v; RebuildVisible(); });
        if (ToggleWarning != null) ToggleWarning.onValueChanged.AddListener(v => { ShowWarning = v; RebuildVisible(); });
        if (ToggleError != null) ToggleError.onValueChanged.AddListener(v => { ShowError = v; RebuildVisible(); });
        if (ToggleSuccess != null) ToggleSuccess.onValueChanged.AddListener(v => { ShowSuccess = v; RebuildVisible(); });
        if (ToggleDebug != null) ToggleDebug.onValueChanged.AddListener(v => { ShowDebug = v; RebuildVisible(); });
    }

    // ─── Публичные методы добавления ─────────────────────────────────────────
    public void Info(string msg) => AddLog(msg, LogType.Info);
    public void Warning(string msg) => AddLog(msg, LogType.Warning);
    public void Error(string msg) => AddLog(msg, LogType.Error);
    public void Success(string msg) => AddLog(msg, LogType.Success);
    public void Debug(string msg) => AddLog(msg, LogType.Debug);

    /// <summary>Добавить лог с явным указанием типа.</summary>
    public void AddLog(string message, LogType type = LogType.Info)
    {
        // ── Создаём запись ──────────────────────────────────────────────────
        var entry = new LogEntry
        {
            Type = type,
            Message = message,
            Timestamp = System.DateTime.Now.ToString("HH:mm:ss"),
        };

        // ── Создаём GameObject только если тип сейчас видим ────────────────
        entry.GameObject = IsTypeVisible(type)
            ? CreateLineObject(entry)
            : null;

        _entries.AddLast(entry);

        // ── Удаляем старые, если превысили лимит ────────────────────────────
        while (_entries.Count > MaxLogs)
        {
            var oldest = _entries.First.Value;
            if (oldest.GameObject != null)
                Destroy(oldest.GameObject);
            _entries.RemoveFirst();
        }

        // ── Авто-прокрутка вниз ─────────────────────────────────────────────
        if (AutoScroll && ScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            ScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // ─── Очистить всё ────────────────────────────────────────────────────────
    public void Clear()
    {
        foreach (var e in _entries)
            if (e.GameObject != null) Destroy(e.GameObject);

        _entries.Clear();
    }

    // ─── Пересборка видимых строк при смене фильтров ─────────────────────────
    private void RebuildVisible()
    {
        foreach (var entry in _entries)
        {
            bool shouldShow = IsTypeVisible(entry.Type);

            if (shouldShow && entry.GameObject == null)
            {
                // Нужно показать — создаём объект
                entry.GameObject = CreateLineObject(entry);

                // Восстанавливаем порядок: ставим в правильную позицию по индексу
                int idx = GetEntryIndex(entry);
                entry.GameObject.transform.SetSiblingIndex(idx);
            }
            else if (!shouldShow && entry.GameObject != null)
            {
                // Нужно скрыть — уничтожаем объект
                Destroy(entry.GameObject);
                entry.GameObject = null;
            }
        }
    }

    // ─── Создание одного текстового объекта ──────────────────────────────────
    private GameObject CreateLineObject(LogEntry entry)
    {
        GameObject go;

        if (LogLinePrefab != null)
        {
            go = Instantiate(LogLinePrefab, LogContainer);
        }
        else
        {
            // Создаём строку без префаба
            go = new GameObject($"Log_{entry.Type}");
            go.transform.SetParent(LogContainer, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(0, FontSize + 6f);

            go.AddComponent<CanvasRenderer>();
        }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();

        // ── Формируем текст строки ──────────────────────────────────────────
        string prefix = LogPrefixes[entry.Type];
        string timestamp = ShowTimestamp ? $"[{entry.Timestamp}] " : "";
        tmp.text = $"{timestamp}{prefix}{entry.Message}";
        tmp.color = LogColors[entry.Type];
        tmp.fontSize = FontSize;
        tmp.raycastTarget = false;

        return go;
    }

    // ─── Вспомогательные ─────────────────────────────────────────────────────
    private bool IsTypeVisible(LogType type) => type switch
    {
        LogType.Info => ShowInfo,
        LogType.Warning => ShowWarning,
        LogType.Error => ShowError,
        LogType.Success => ShowSuccess,
        LogType.Debug => ShowDebug,
        _ => true,
    };

    /// <summary>Считаем сколько видимых записей стоит перед данной — для SetSiblingIndex.</summary>
    private int GetEntryIndex(LogEntry target)
    {
        int idx = 0;
        foreach (var e in _entries)
        {
            if (e == target) break;
            if (e.GameObject != null) idx++;
        }
        return idx;
    }
}