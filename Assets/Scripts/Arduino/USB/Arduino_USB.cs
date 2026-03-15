using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ArduinoController_Connect : MonoBehaviour
{
    public bool isConnected = false;

    public event Action<string> OnMessageReceived;
    public event Action<ConnectionState> OnStateChanged;
    public static ArduinoController_Connect Instance { get; private set; }

    // ── Данные с датчиков — читай из любого скрипта ───────────────────────
    public float CurrentLevelCm { get; private set; } = 0f;
    public float CurrentRawLevel { get; private set; } = 0f;
    public float CurrentTempC { get; private set; } = 0f;
    public string CurrentStatus { get; private set; } = "";
    public int CurrentPumpPct { get; private set; } = 0;
    public int CurrentHeater { get; private set; } = 0;
    public int CurrentFlcPct { get; private set; } = 0;

    private const int BaudRate = 115200;
    private const float WaitAfterOpen = 2.0f;
    private const float ResponseTimeout = 3.0f;
    private const float WatchdogTimeout = 20.0f;

    [Header("Параметры среды")]
    public float LevelMin = 1.0f;
    public float LevelMax = 1.5f;
    public float LevelCritical = 2.0f;
    public bool AutoModeOnStart = true;
    [Range(0, 255)] public int PumpPwmOnStart = 255;

    [Header("UI")]
    [SerializeField] private RawImage StatusImage;
    [SerializeField] private Button ReconnectButton;
    [SerializeField] private Button StopSearchButton;
    [SerializeField] private Button DisconnectButton;
    [SerializeField] private GameObject ConnectionPanel;

    [Header("Лог")]
    [SerializeField] private LogPanel[] _logs;

    private static readonly Color ColorRed = new Color(0.90f, 0.20f, 0.20f);
    private static readonly Color ColorYellow = new Color(0.95f, 0.80f, 0.10f);
    private static readonly Color ColorGreen = new Color(0.20f, 0.85f, 0.30f);
    private static readonly Color ColorOff = new Color(0.15f, 0.15f, 0.15f);

    public enum ConnectionState { Idle, Scanning, Connecting, Initializing, Connected, Lost }

    private SerialPort _port;
    private string _buffer = "";
    private ConnectionState _state = ConnectionState.Idle;
    private float _lastTelemetryTime;

    private Coroutine _blinkCoroutine;
    private Coroutine _connectCoroutine;

    private readonly Queue<(string cmd, Action<bool> cb)> _cmdQueue =
        new Queue<(string, Action<bool>)>();
    private bool _cmdBusy = false;

    private const string IGNORED_RESPONSE = "OK:DATA";

    // =========================================================================
    void Awake() => Instance = this;

    void Start()
    {
        ReconnectButton?.onClick.AddListener(OnReconnectClicked);
        StopSearchButton?.onClick.AddListener(OnStopSearchClicked);
        DisconnectButton?.onClick.AddListener(OnDisconnectClicked);
        UpdateButtons();
    }

    // ── Лог-обёртки ──────────────────────────────────────────────────────────
    private void LogInfo(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Info(msg); }
    private void LogWarning(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Warning(msg); }
    private void LogError(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Error(msg); }
    private void LogSuccess(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Success(msg); }
    private void LogDebug(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Debug(msg); }

    // ── Кнопки ───────────────────────────────────────────────────────────────
    private void UpdateButtons()
    {
        bool searching = _state == ConnectionState.Scanning ||
                         _state == ConnectionState.Connecting ||
                         _state == ConnectionState.Initializing;
        bool conn = _state == ConnectionState.Connected;

        if (ReconnectButton) ReconnectButton.interactable = !searching && !conn;
        if (StopSearchButton) StopSearchButton.interactable = searching;
        if (DisconnectButton) DisconnectButton.interactable = conn;
        if (ConnectionPanel) ConnectionPanel.SetActive(!conn);
    }

    public void OnReconnectClicked()
    {
        LogInfo("Запуск подключения...");
        Disconnect(); SetState(ConnectionState.Idle);
        _connectCoroutine = StartCoroutine(AutoConnectRoutine());
    }

    public void OnStopSearchClicked()
    {
        LogWarning("Поиск остановлен вручную.");
        Disconnect(); SetState(ConnectionState.Idle);
    }

    public void OnDisconnectClicked()
    {
        LogWarning("Отключение по запросу пользователя.");
        Disconnect(); SetState(ConnectionState.Idle);
    }

    private void Disconnect()
    {
        StopAllCoroutines();
        _blinkCoroutine = null;
        try { _port?.Close(); } catch { }
        _port = null;
        isConnected = false;
        _cmdBusy = false;
        _cmdQueue.Clear();
    }

    public void ConnectToPort(string portName)
    {
        if (isConnected) { LogWarning("Уже подключено."); return; }
        Disconnect(); SetState(ConnectionState.Idle);
        _connectCoroutine = StartCoroutine(SinglePortConnectRoutine(portName));
    }

    public void PushConfig()
    {
        if (!isConnected) { LogWarning("PushConfig: не подключено."); return; }
        SendCommand(BuildConfigCommand(), ok =>
            LogInfo(ok ? "CONFIG обновлён." : "Ошибка CONFIG."));
    }

    private IEnumerator SinglePortConnectRoutine(string portName)
    {
        yield return StartCoroutine(TryConnect(portName));
        if (!isConnected) { LogError($"Не удалось подключиться к {portName}."); SetState(ConnectionState.Idle); }
    }

    // ── Индикатор ────────────────────────────────────────────────────────────
    private void SetState(ConnectionState next)
    {
        if (_state == next) return;
        _state = next;
        OnStateChanged?.Invoke(next);

        if (_blinkCoroutine != null) { StopCoroutine(_blinkCoroutine); _blinkCoroutine = null; }

        switch (next)
        {
            case ConnectionState.Scanning: SetImageColor(ColorYellow); break;
            case ConnectionState.Connecting:
            case ConnectionState.Initializing: _blinkCoroutine = StartCoroutine(BlinkRoutine(ColorYellow, 0.4f)); break;
            case ConnectionState.Connected: SetImageColor(ColorGreen); break;
            default: SetImageColor(ColorRed); break;
        }
        UpdateButtons();
    }

    private void SetImageColor(Color c) { if (StatusImage) StatusImage.color = c; }

    private IEnumerator BlinkRoutine(Color on, float interval)
    {
        bool t = true;
        while (true) { SetImageColor(t ? on : ColorOff); t = !t; yield return new WaitForSeconds(interval); }
    }

    // ── Авто-поиск ───────────────────────────────────────────────────────────
    private IEnumerator AutoConnectRoutine()
    {
        while (!isConnected)
        {
            SetState(ConnectionState.Scanning);
            string[] ports = SerialPort.GetPortNames()
                .Where(p => p.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => { int.TryParse(p.Substring(3), out int n); return n; })
                .ToArray();

            if (ports.Length == 0)
            {
                LogWarning("COM-портов не обнаружено. Повтор через 3 сек...");
                yield return new WaitForSeconds(3f); continue;
            }

            foreach (string p in ports)
            {
                yield return StartCoroutine(TryConnect(p));
                if (isConnected) yield break;
            }

            LogWarning("Arduino не обнаружена. Повтор через 3 сек...");
            yield return new WaitForSeconds(3f);
        }
    }

    private IEnumerator TryConnect(string portName)
    {
        SetState(ConnectionState.Connecting);
        LogInfo($"Пробую {portName}...");

        SerialPort port = new SerialPort(portName, BaudRate)
        {
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 200,
            WriteTimeout = 200,
            Encoding = Encoding.UTF8,
        };

        try { port.Open(); }
        catch (Exception e) { LogError($"{portName}: {e.Message}"); yield break; }

        port.DiscardInBuffer(); port.DiscardOutBuffer();
        SetState(ConnectionState.Initializing);

        float t = 0f;
        while (t < WaitAfterOpen) { t += Time.deltaTime; yield return null; }
        port.DiscardInBuffer();

        WriteToPort(port, "HELLO");
        string resp = ""; float lt = 0f;
        while (lt < 6f)
        {
            try { resp += port.ReadExisting(); } catch { }
            if (resp.Contains("HELLO:READY")) break;
            lt += Time.deltaTime; yield return null;
        }
        if (!resp.Contains("HELLO:READY"))
        {
            LogWarning($"{portName} — нет HELLO:READY.");
            port.Close(); SetState(ConnectionState.Scanning); yield break;
        }

        WriteToPort(port, BuildConfigCommand());
        string cfgBuf = ""; float ct = 0f;
        while (ct < 3f)
        {
            try { cfgBuf += port.ReadExisting(); } catch { }
            if (cfgBuf.Contains("OK:CONFIG")) break;
            ct += Time.deltaTime; yield return null;
        }

        LogSuccess($"Подключено: {portName}");
        _port = port;
        isConnected = true;
        _lastTelemetryTime = Time.time;
        SetState(ConnectionState.Connected);
        StartCoroutine(ReadLoop());
        StartCoroutine(WatchdogLoop());
    }

    // ── Watchdog ─────────────────────────────────────────────────────────────
    private IEnumerator WatchdogLoop()
    {
        while (isConnected)
        {
            yield return new WaitForSeconds(1f);
            if (Time.time - _lastTelemetryTime >= WatchdogTimeout)
            {
                LogError($"Нет ответа {WatchdogTimeout} сек. Переподключение...");
                HandleDisconnect(); yield break;
            }
        }
    }

    private void HandleDisconnect()
    {
        SetState(ConnectionState.Lost);
        isConnected = false; _cmdBusy = false; _cmdQueue.Clear();
        try { _port?.Close(); } catch { }
        _port = null;
        _connectCoroutine = StartCoroutine(AutoConnectRoutine());
    }

    // ── Чтение + парсинг ─────────────────────────────────────────────────────
    private IEnumerator ReadLoop()
    {
        while (_port != null && _port.IsOpen)
        {
            string incoming = "";
            try { incoming = _port.ReadExisting(); } catch { }

            if (!string.IsNullOrEmpty(incoming))
            {
                _lastTelemetryTime = Time.time;
                _buffer += incoming;
            }

            int nl;
            while ((nl = _buffer.IndexOf('\n')) >= 0)
            {
                string line = _buffer.Substring(0, nl).Trim();
                _buffer = _buffer.Substring(nl + 1);
                if (string.IsNullOrEmpty(line)) continue;

                Debug.Log($"[Arduino] <- {line}");

                bool isDataAck = string.Equals(line, IGNORED_RESPONSE,
                    StringComparison.OrdinalIgnoreCase);

                if (!isDataAck) LogInfo(line);

                ParseTelemetry(line);

                if (_cmdBusy && !isDataAck &&
                    (line.StartsWith("OK:") || line.StartsWith("ERROR:")))
                {
                    _cmdBusy = false;
                    StartCoroutine(FireCallbackAndNext(line.StartsWith("OK:")));
                }

                if (!isDataAck) OnMessageReceived?.Invoke(line);
            }

            yield return null;
        }

        LogWarning("Порт закрылся.");
        HandleDisconnect();
    }

    // ── Парсер телеметрии ─────────────────────────────────────────────────────
    // Arduino шлёт:
    //   "TEMP: 25.30 C"
    //   "WATER LEVEL RAW: 15"   (значение уже в см: MAX_LEVEL - дистанция)
    //   "CTRL PIN:75 HTR:1 FLC:0"
    private void ParseTelemetry(string line)
    {
        // Температура
        if (line.StartsWith("TEMP:"))
        {
            string s = line.Substring(5).Trim();
            int sp = s.IndexOf(' ');
            if (sp > 0) s = s.Substring(0, sp);
            if (float.TryParse(s,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float v))
                CurrentTempC = v;
            return;
        }

        // Уровень воды — формат: "WATER LEVEL RAW: 15"
        // Значение уже в сантиметрах (Arduino считает MAX_LEVEL - getDist)
        if (line.StartsWith("WATER LEVEL RAW:"))
        {
            string s = line.Substring(16).Trim();
            if (float.TryParse(s,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float cm))
            {
                CurrentRawLevel = cm;   // исходное значение
                CurrentLevelCm = cm;   // уровень в сантиметрах
            }
            return;
        }

        // Статус
        if (line.StartsWith("STATUS:"))
        {
            CurrentStatus = line.Substring(7).Trim();
            return;
        }

        // Управляющие воздействия
        if (line.StartsWith("CTRL"))
        {
            CurrentPumpPct = ParseInt(line, "PIN:");
            CurrentHeater = ParseInt(line, "HTR:");
            CurrentFlcPct = ParseInt(line, "FLC:");
        }
    }

    private static int ParseInt(string line, string key)
    {
        int idx = line.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return 0;
        int s = idx + key.Length;
        int e = line.IndexOf(' ', s);
        string sub = e < 0 ? line.Substring(s) : line.Substring(s, e - s);
        int.TryParse(sub.Trim(), out int v);
        return v;
    }

    // ── Очередь команд ───────────────────────────────────────────────────────
    public void SendCommand(string command, Action<bool> onResult = null)
    {
        if (_port == null || !_port.IsOpen)
        {
            LogWarning($"Не подключено: {command}");
            onResult?.Invoke(false); return;
        }
        _cmdQueue.Enqueue((command, onResult));
        TryProcessQueue();
    }

    private void TryProcessQueue()
    {
        if (_cmdBusy || _cmdQueue.Count == 0) return;
        var (cmd, cb) = _cmdQueue.Peek();
        _cmdBusy = true;
        try
        {
            WriteToPort(_port, cmd);
            LogDebug($"-> {cmd}");
            StartCoroutine(TimeoutGuard(cb));
        }
        catch (Exception e)
        {
            LogError($"Ошибка отправки: {e.Message}");
            _cmdQueue.Dequeue(); _cmdBusy = false;
            cb?.Invoke(false); TryProcessQueue();
        }
    }

    private IEnumerator TimeoutGuard(Action<bool> cb)
    {
        float t = 0f;
        while (_cmdBusy && t < ResponseTimeout) { t += Time.deltaTime; yield return null; }
        if (!_cmdBusy) yield break;

        if (_cmdQueue.Count > 0) LogWarning($"Таймаут: {_cmdQueue.Peek().cmd}");
        _cmdQueue.Dequeue(); _cmdBusy = false;
        cb?.Invoke(false); TryProcessQueue();
    }

    private IEnumerator FireCallbackAndNext(bool ok)
    {
        yield return null;
        if (_cmdQueue.Count > 0) { var (_, cb) = _cmdQueue.Dequeue(); cb?.Invoke(ok); }
        TryProcessQueue();
    }

    public void SendRaw(string message)
    {
        if (_port == null || !_port.IsOpen) return;
        try { WriteToPort(_port, message); }
        catch (Exception e) { Debug.LogWarning($"[Arduino] SendRaw: {e.Message}"); }
    }

    private string BuildConfigCommand() =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "CONFIG MIN:{0} MAX:{1} CRIT:{2} AUTO:{3} PWM:{4}",
            LevelMin, LevelMax, LevelCritical,
            AutoModeOnStart ? 1 : 0, PumpPwmOnStart);

    private static void WriteToPort(SerialPort port, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message.Trim() + "\n");
        port.Write(data, 0, data.Length);
    }

    public ConnectionState CurrentState => _state;

    void OnDestroy() { if (_port != null && _port.IsOpen) _port.Close(); }
}