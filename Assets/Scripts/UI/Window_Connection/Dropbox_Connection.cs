using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArduinoPortSelector : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown PortDropdown;
    [SerializeField] private Button RefreshButton;
    [SerializeField] private Button ConnectButton;

    [Header("Лог")]
    // Заменили одиночную панель на массив
    [SerializeField] private LogPanel[] _logs;

    [Header("Настройки")]
    [SerializeField] private bool ScanOnStart = true;
    [SerializeField] private float AutoRefreshInterval = 0f;

    private List<string> _portList = new List<string>();

    void Start()
    {
        RefreshButton?.onClick.AddListener(ScanPorts);
        ConnectButton?.onClick.AddListener(ConnectToSelected);
        if (ScanOnStart) ScanPorts();
        if (AutoRefreshInterval > 0f) StartCoroutine(AutoRefreshRoutine());
    }

    // ─── Обертки для мульти-логирования ──────────────────────────────────────
    private void LogInfo(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Info(msg); }
    private void LogWarning(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Warning(msg); }
    private void LogError(string msg) { if (_logs == null) return; foreach (var log in _logs) log?.Error(msg); }

    // ─── Сканирование ────────────────────────────────────────────────────────
    public void ScanPorts()
    {
        _portList = SerialPort.GetPortNames()
            .Where(p => p.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => { int.TryParse(p.Substring(3), out int n); return n; })
            .ToList();

        RefreshDropdown();
        LogInfo(_portList.Count > 0
            ? $"Найдено портов: {string.Join(", ", _portList)}"
            : "COM-портов не обнаружено.");
    }

    private void RefreshDropdown()
    {
        if (PortDropdown == null) return;
        string prev = GetSelectedPort();
        PortDropdown.ClearOptions();

        if (_portList.Count == 0)
        {
            PortDropdown.AddOptions(new List<string> { "— нет портов —" });
            PortDropdown.interactable = false;
            if (ConnectButton) ConnectButton.interactable = false;
            return;
        }

        PortDropdown.AddOptions(_portList.Select(p => new TMP_Dropdown.OptionData(p)).ToList());
        PortDropdown.interactable = true;
        int idx = _portList.IndexOf(prev);
        PortDropdown.value = idx >= 0 ? idx : 0;
        PortDropdown.RefreshShownValue();
        if (ConnectButton) ConnectButton.interactable = true;
    }

    // ─── Подключение ─────────────────────────────────────────────────────────
    public void ConnectToSelected()
    {
        string port = GetSelectedPort();
        if (string.IsNullOrEmpty(port)) { LogWarning("Порт не выбран."); return; }

        var ctrl = ArduinoController_Connect.Instance;
        if (ctrl == null) { LogError("ArduinoController_Connect не найден!"); return; }
        if (ctrl.isConnected) { LogWarning("Уже подключено."); return; }

        LogInfo($"Подключаемся к {port}...");
        StartCoroutine(ConnectRoutine(port, ctrl));
    }

    private IEnumerator ConnectRoutine(string portName, ArduinoController_Connect ctrl)
    {
        SetButtonsInteractable(false);

        bool done = false;
        void OnState(ArduinoController_Connect.ConnectionState s)
        {
            if (s == ArduinoController_Connect.ConnectionState.Connected ||
                s == ArduinoController_Connect.ConnectionState.Lost ||
                s == ArduinoController_Connect.ConnectionState.Idle)
                done = true;
        }

        ctrl.OnStateChanged += OnState;
        ctrl.ConnectToPort(portName);

        float t = 0f;
        while (!done && t < 30f) { t += Time.deltaTime; yield return null; }
        ctrl.OnStateChanged -= OnState;

        SetButtonsInteractable(true);
        LogInfo(ctrl.isConnected ? $"Подключено: {portName}" : $"Не удалось: {portName}");
    }

    private IEnumerator AutoRefreshRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(AutoRefreshInterval);
            var ctrl = ArduinoController_Connect.Instance;
            if (ctrl == null || !ctrl.isConnected) ScanPorts();
        }
    }

    public string GetSelectedPort()
    {
        if (PortDropdown == null || _portList.Count == 0) return null;
        int i = PortDropdown.value;
        return i >= 0 && i < _portList.Count ? _portList[i] : null;
    }

    private void SetButtonsInteractable(bool v)
    {
        if (RefreshButton) RefreshButton.interactable = v;
        if (ConnectButton) ConnectButton.interactable = v;
        if (PortDropdown) PortDropdown.interactable = v;
    }
}