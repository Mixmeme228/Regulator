using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ArduinoDataReceiver : MonoBehaviour
{
    [Header("Панели устройств (ManualDeviceControl)")]
    [SerializeField] private ManualDeviceControl _waterPump;
    [SerializeField] private ManualDeviceControl _reagentPump;
    [SerializeField] private ManualDeviceControl _heater;
    [SerializeField] private ManualDeviceControl _drainValve;

    [Header("Метки датчиков на схеме")]
    [SerializeField] private TextMeshProUGUI _labelTemperature;   // "0°"
    [SerializeField] private TextMeshProUGUI _labelLevel;         // "0 см" (первая)
    [SerializeField] private TextMeshProUGUI _labelLevel2;        // "0 см" (вторая, если есть)
    [SerializeField] private TextMeshProUGUI _labelBio;           // "0 см" биозагрязнение
    [SerializeField] private TextMeshProUGUI _labelFlocculant;    // "0 см" флокулянт

    [Header("Arduino")]
    [SerializeField] private ArduinoController_Connect _arduino;

   
    void OnEnable()
    {
        if (_arduino == null) _arduino = ArduinoController_Connect.Instance;
        if (_arduino != null) _arduino.OnMessageReceived += Parse;
    }

    void OnDisable()
    {
        if (_arduino != null) _arduino.OnMessageReceived -= Parse;
    }

    
    private void Parse(string line)
    {
        
        if (line.StartsWith("CTRL"))
        {
            int pin = ParseInt(line, "PIN:");
            int htr = ParseInt(line, "HTR:");
            int vlv = ParseInt(line, "VLV:");
            int flc = ParseInt(line, "FLC:");

            _waterPump?.OnCtrlReceived(pin);
            _heater?.OnCtrlReceived(htr);
            _drainValve?.OnCtrlReceived(vlv);
            _reagentPump?.OnCtrlReceived(flc);
            return;
        }

        
        if (line.StartsWith("TEMP:"))
        {
            if (_labelTemperature == null) return;
            string val = line.Substring(5).Trim();
            if (val.StartsWith("ERROR"))
                _labelTemperature.text = "ERR";
            else
            {
                // "25.50 C" → берём первое слово
                string num = val.Split(' ')[0];
                if (float.TryParse(num, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float t))
                    _labelTemperature.text = t.ToString("F1") + " C";
            }
            return;
        }

        
        if (line.StartsWith("WATER LEVEL:"))
        {
            if (_labelLevel == null) return;
            string after = line.Substring(12).Trim();  // "1.23 cm ..."
            string num = after.Split(' ')[0];
            if (float.TryParse(num, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float lvl))
            {
                string txt = lvl.ToString("F2") + " см";
                if (_labelLevel != null) _labelLevel.text = txt;
                if (_labelLevel2 != null) _labelLevel2.text = txt;
            }
            return;
        }

        if (line.Contains("AUTO MODE ON"))
        {
            GlobalModeControl.Instance?.ForceState(isAuto: true);
            return;
        }
        if (line.Contains("AUTO MODE OFF"))
        {
            GlobalModeControl.Instance?.ForceState(isAuto: false);
            return;
        }
    }

    private static int ParseInt(string msg, string key)
    {
        int idx = msg.IndexOf(key);
        if (idx < 0) return 0;
        int s = idx + key.Length;
        int e = msg.IndexOf(' ', s);
        if (e < 0) e = msg.Length;
        return int.TryParse(msg.Substring(s, e - s), out int v) ? v : 0;
    }
}