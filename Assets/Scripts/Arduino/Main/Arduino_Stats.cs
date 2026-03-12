using UnityEngine;

public class Arduino_Stats : MonoBehaviour
{
    [Header("Последние данные с Arduino")]
    [SerializeField] private float temperature = 0f;
    [SerializeField] private float waterLevelCm = 0f;
    [SerializeField] private int waterLevelRaw = 0;
    [SerializeField] private bool autoMode = false;
    [SerializeField] private int pumpPwm = 0;
    [SerializeField] private bool criticalLevel = false;

    
    public float Temperature => temperature;
    public float WaterLevelCm => waterLevelCm;
    public int WaterLevelRaw => waterLevelRaw;
    public bool AutoMode => autoMode;
    public int PumpPwm => pumpPwm;
    public bool CriticalLevel => criticalLevel;

    void OnEnable()
    {
       
        if (ArduinoController_Connect.Instance != null)
            ArduinoController_Connect.Instance.OnMessageReceived += OnMessage;
    }

    void OnDisable()
    {
        if (ArduinoController_Connect.Instance != null)
            ArduinoController_Connect.Instance.OnMessageReceived -= OnMessage;
    }

    private void OnMessage(string line)
    {
        
        if (line.StartsWith("DATA:"))
        {
            ParseData(line.Substring(5));
            criticalLevel = false;
        }
       
        else if (line.StartsWith("CRITICAL:"))
        {
            criticalLevel = true;
            Debug.LogWarning("[Stats] Критический уровень воды!");
        }
    }

    private void ParseData(string payload)
    {
        
        string[] tokens = payload.Trim()
            .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            int eq = token.IndexOf('=');
            if (eq < 0) continue;

            string key = token.Substring(0, eq).ToLower();
            string val = token.Substring(eq + 1);

            switch (key)
            {
                case "temp":
                    if (float.TryParse(val,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float t))
                        temperature = t;
                    break;

                case "level":
                    if (float.TryParse(val,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float l))
                        waterLevelCm = l;
                    break;

                case "raw":
                    if (int.TryParse(val, out int r))
                        waterLevelRaw = r;
                    break;

                case "automode":
                    autoMode = val == "1";
                    break;

                case "pumppwm":
                    if (int.TryParse(val, out int p))
                        pumpPwm = p;
                    break;
            }
        }
    }
}