using System;
using System.Globalization;
using UnityEngine;

public class FlotationPlantModel : MonoBehaviour
{
    [Header("Физические константы")]
    public const float MaxVolume = 5.0f;
    public const float WaterDensity = 1000f;
    public const float SpecificHeatWater = 4187f;

    public float heatTransferCoef = 5.0f;
    public float surfaceArea = 0.15f;

    [Header("Инерция системы")]
    public float heaterTimeConstant = 1.5f;
    public float sensorTempTimeConstant = 0.5f;
    public float sensorVolTimeConstant = 1.0f;

    private float _actualHeaterPower = 0f;
    private float _sensorTemperature = 15f;
    private float _sensorVolume = 0.001f;

    [Header("Состояние системы")]
    public float currentVolume = 0.001f;
    public float currentTemperature = 15.0f;
    public float currentBioConcentration = 100f;
    public float currentFlocculantConc = 0.0f;

    [Header("Показания датчиков (инертные)")]
    [SerializeField] private float dbg_SensorTemperature;
    [SerializeField] private float dbg_SensorVolume;
    [SerializeField] private float dbg_ActualHeaterPower;

    [Header("Параметры окружения")]
    public float ambientTemperature = 20.0f;
    public float inletWaterTemp = 10.0f;
    public float inletBioConc = 150.0f;

    [Header("Управляющие воздействия (0..1)")]
    [Range(0, 1)] public float u_PumpIn = 0f;
    [Range(0, 1)] public float u_Heater = 0f;
    [Range(0, 1)] public float u_ValveOut = 0f;
    [Range(0, 1)] public float u_Aeration = 0f;
    [Range(0, 1)] public float u_Flocculant = 0f;

    [Header("Характеристики узлов")]
    public float maxFlowRate = 0.2f;
    public float maxHeaterPower = 500.0f;
    public float flocculantSourceConc = 1000.0f;
    public float baseCleaningRate = 0.15f;

    [Header("Уставки процесса")]
    public float setpointTemperature = 30.0f;
    public float setpointVolume = 3.0f;
    public float setpointBio = 5.0f;

    [Header("Связь с Arduino")]
    [Range(0.05f, 2.0f)] public float sendInterval = 0.1f;
    [SerializeField] private ArduinoController_Connect _arduino;

    private float _sendTimer = 0f;
    private bool _configSent = false;
    private float _prevSpTemp = float.NaN;
    private float _prevSpVol = float.NaN;
    private float _prevSpBio = float.NaN;
    private float _prevAmbient = float.NaN;
    private const float SP_EPS = 0.01f;

    // =========================================================================
    void Awake()
    {
        if (_arduino == null)
            Debug.LogError("[PlantModel] ArduinoController_Connect не назначен!");
        _sensorTemperature = currentTemperature;
        _sensorVolume = currentVolume;
    }

    void OnEnable()
    {
        if (_arduino == null) return;
        _arduino.OnMessageReceived += HandleArduinoMessage;
        _arduino.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        if (_arduino == null) return;
        _arduino.OnMessageReceived -= HandleArduinoMessage;
        _arduino.OnStateChanged -= HandleStateChanged;
    }

    void Update()
    {
        dbg_SensorTemperature = _sensorTemperature;
        dbg_SensorVolume = _sensorVolume;
        dbg_ActualHeaterPower = _actualHeaterPower;

        if (_arduino == null || !_arduino.isConnected) return;

        // DATA — только в АВТ режиме, чтобы не перезапускать ПИ в ручном
        _sendTimer += Time.deltaTime;
        if (_sendTimer >= sendInterval)
        {
            _sendTimer = 0f;
            if (GlobalModeControl.IsAutoMode)
                SendSensorData();
        }

        if (!_configSent) return;

        bool changed =
            Mathf.Abs(setpointTemperature - _prevSpTemp) > SP_EPS ||
            Mathf.Abs(setpointVolume - _prevSpVol) > SP_EPS ||
            Mathf.Abs(setpointBio - _prevSpBio) > SP_EPS ||
            Mathf.Abs(ambientTemperature - _prevAmbient) > SP_EPS;
        if (changed) SendConfig();
    }

    // =========================================================================
    //  ФИЗИКА
    // =========================================================================
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        float flowIn = maxFlowRate * u_PumpIn;
        float flowReag = maxFlowRate * 0.05f * u_Flocculant;
        float flowOut = maxFlowRate * u_ValveOut;

        currentVolume = Mathf.Clamp(
            currentVolume + (flowIn + flowReag - flowOut) * dt,
            0.001f, MaxVolume);

        _sensorVolume += (currentVolume - _sensorVolume)
                         * (dt / Mathf.Max(0.01f, sensorVolTimeConstant));

        float tauHtr = Mathf.Max(0.01f, heaterTimeConstant);
        _actualHeaterPower += (maxHeaterPower * u_Heater - _actualHeaterPower) * (dt / tauHtr);

        if (currentVolume > 0.1f)
        {
            float mass = currentVolume * (WaterDensity / 1000f);
            float energyAdded = _actualHeaterPower * dt;
            float energyInflow = flowIn * dt * (WaterDensity / 1000f)
                                 * SpecificHeatWater * (inletWaterTemp - currentTemperature);
            float heatLoss = heatTransferCoef * surfaceArea
                                 * Mathf.Max(0f, currentTemperature - ambientTemperature) * dt;
            currentTemperature += (energyAdded + energyInflow - heatLoss)
                                  / (mass * SpecificHeatWater);
        }

        _sensorTemperature += (currentTemperature - _sensorTemperature)
                              * (dt / Mathf.Max(0.01f, sensorTempTimeConstant));

        if (currentVolume > 0.001f)
        {
            float dCf = ((flowReag * flocculantSourceConc
                         - flowOut * currentFlocculantConc) / currentVolume) * dt;
            currentFlocculantConc = Mathf.Max(0f, currentFlocculantConc + dCf);
        }

        if (currentVolume > 0.1f)
        {
            float bioInflow = (flowIn * inletBioConc
                              - flowOut * currentBioConcentration) / currentVolume;
            float flocMult = 1f + 2f * (currentFlocculantConc / (currentFlocculantConc + 10f));
            float removal = baseCleaningRate * u_Aeration * flocMult * currentBioConcentration;
            currentBioConcentration = Mathf.Max(0f,
                currentBioConcentration + (bioInflow - removal) * dt);
        }
    }

    // =========================================================================
    //  СВЯЗЬ
    // =========================================================================
    private void SendSensorData()
    {
        float bioNoise = currentBioConcentration + UnityEngine.Random.Range(-0.5f, 0.5f);
        _arduino.SendRaw(string.Format(CultureInfo.InvariantCulture,
            "DATA T:{0:F3} V:{1:F3} BIO:{2:F3} CF:{3:F3}",
            _sensorTemperature, _sensorVolume, bioNoise, currentFlocculantConc));
    }

    public void SendConfig()
    {
        if (_arduino == null || !_arduino.isConnected) return;
        string cmd = string.Format(CultureInfo.InvariantCulture,
            "CONFIG AMB:{0} SPT:{1} SPV:{2} SPBIO:{3}",
            ambientTemperature, setpointTemperature, setpointVolume, setpointBio);
        _arduino.SendRaw(cmd);
        _prevSpTemp = setpointTemperature;
        _prevSpVol = setpointVolume;
        _prevSpBio = setpointBio;
        _prevAmbient = ambientTemperature;
        _configSent = true;
        Debug.Log($"[PlantModel] CONFIG → {cmd}");
    }

    private void HandleArduinoMessage(string line)
    {
        // ── Лог всех входящих для отладки ────────────────────
        Debug.Log($"[PlantModel] MSG: '{line}'");

        // ── CTRL от ПИ-регулятора ─────────────────────────────
        if (line.StartsWith("CTRL"))
        {
            u_PumpIn = ParseKey(line, "PIN:");
            u_Heater = ParseKey(line, "HTR:");
            u_ValveOut = ParseKey(line, "VLV:");
            u_Aeration = ParseKey(line, "AER:");
            u_Flocculant = ParseKey(line, "FLC:");
            return;
        }

        // ── Нагреватель ───────────────────────────────────────
        if (line.Contains("HEATER ON")) { u_Heater = 1f; Debug.Log("[PlantModel] u_Heater=1"); return; }
        if (line.Contains("HEATER OFF")) { u_Heater = 0f; Debug.Log("[PlantModel] u_Heater=0"); return; }

        // ── Насос воды — все возможные форматы ────────────────
        // Новый формат: "OK: WATER PUMP 50% (AUTO OFF)"
        // Старый формат: "OK: WATER ON" / "OK: WATER OFF"
        if (line.Contains("WATER") && !line.Contains("LEVEL") && !line.Contains("AUTO:"))
        {
            if (line.Contains("OFF")) { u_PumpIn = 0f; Debug.Log("[PlantModel] u_PumpIn=0"); }
            else if (line.Contains("50%")) { u_PumpIn = 0.5f; Debug.Log("[PlantModel] u_PumpIn=0.5"); }
            else if (line.Contains("70%")) { u_PumpIn = 0.7f; Debug.Log("[PlantModel] u_PumpIn=0.7"); }
            else if (line.Contains("100%") || line.Contains("ON")) { u_PumpIn = 1.0f; Debug.Log("[PlantModel] u_PumpIn=1"); }
            return;
        }

        // ── Насос реагентов ───────────────────────────────────
        if (line.Contains("REAGENT"))
        {
            if (line.Contains("OFF")) { u_Flocculant = 0f; Debug.Log("[PlantModel] u_Flocculant=0"); }
            else if (line.Contains("50%")) { u_Flocculant = 0.5f; Debug.Log("[PlantModel] u_Flocculant=0.5"); }
            else if (line.Contains("70%")) { u_Flocculant = 0.7f; Debug.Log("[PlantModel] u_Flocculant=0.7"); }
            else if (line.Contains("100%") || line.Contains("ON")) { u_Flocculant = 1.0f; Debug.Log("[PlantModel] u_Flocculant=1"); }
            return;
        }

        // ── Клапан ────────────────────────────────────────────
        if (line.Contains("VALVE"))
        {
            if (line.Contains("OPEN")) { u_ValveOut = 1f; Debug.Log("[PlantModel] u_ValveOut=1"); }
            else if (line.Contains("CLOSE")) { u_ValveOut = 0f; Debug.Log("[PlantModel] u_ValveOut=0"); }
            return;
        }

        // ── AUTO OFF — НЕ сбрасываем выходы модели
        // Arduino делает allOff() сама на железе, но модель должна
        // сохранить последние значения чтобы UI не мигал нулями.
        // ManualDeviceControl.holdState защищает UI от временных нулей CTRL.
    }

    private void HandleStateChanged(ArduinoController_Connect.ConnectionState state)
    {
        if (state == ArduinoController_Connect.ConnectionState.Connected && !_configSent)
            Invoke(nameof(SendConfig), 0.5f);

        if (state == ArduinoController_Connect.ConnectionState.Lost ||
            state == ArduinoController_Connect.ConnectionState.Idle)
        {
            _configSent = false;
            u_PumpIn = u_Heater = u_ValveOut = u_Aeration = u_Flocculant = 0f;
        }
    }

    public void SetTargetTemperature(float v) { setpointTemperature = Mathf.Clamp(v, ambientTemperature, 80f); SendConfig(); }
    public void SetTargetVolume(float v) { setpointVolume = Mathf.Clamp(v, 0.1f, MaxVolume); SendConfig(); }
    public void SetTargetBio(float v) { setpointBio = Mathf.Max(0f, v); SendConfig(); }

    private static float ParseKey(string msg, string key, float def = 0f)
    {
        int idx = msg.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return def;
        int s = idx + key.Length;
        int e = msg.IndexOf(' ', s);
        if (e < 0) e = msg.Length;
        return float.TryParse(msg.Substring(s, e - s),
            NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? Mathf.Clamp01(v) : def;
    }
}