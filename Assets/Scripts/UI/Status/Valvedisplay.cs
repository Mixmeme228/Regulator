using UnityEngine;
using Unity.VectorGraphics;

/// <summary>
/// Отображает состояние клапана по ГОСТ-таблице.
/// Работает с SVGImage (Unity Vector Graphics).
/// </summary>
public class ValveDisplay : MonoBehaviour
{
    public enum ValveState
    {
        Closed,
        StartingOpen,
        Opening,
        Open,
        StartingClose,
        Closing,
        Stopped,
        Unreliable
    }

    // =========================================================================
    [Header("Зависимости")]
    [SerializeField] private FlotationPlantModel _model;

    [Header("Треугольники клапана (SVGImage)")]
    [SerializeField] private SVGImage _leftTriangle;
    [SerializeField] private SVGImage _rightTriangle;

    [Header("Диагностика")]
    [SerializeField] private ValveState _currentState = ValveState.Closed;

    [Header("Настройки")]
    [Tooltip("Время состояния 'Трогается', сек")]
    public float transitionTime = 0.8f;

    [Tooltip("Порог u_ValveOut для считания клапана открытым")]
    [Range(0.01f, 0.5f)]
    public float openThreshold = 0.05f;

    // ── Цвета ────────────────────────────────────────────────
    private static readonly Color ColGreen = new Color(0.05f, 0.85f, 0.15f, 1f);
    private static readonly Color ColRed = new Color(1f, 0.08f, 0.05f, 1f);
    private static readonly Color ColWhite = Color.white;

    // ── Приватные ────────────────────────────────────────────
    private float _prevValve = 0f;
    private float _transTimer = 0f;
    private float _blinkTimer = 0f;

    // =========================================================================
    void Start()
    {
        if (_model == null)
        {
            _model = FindObjectOfType<FlotationPlantModel>();
            if (_model == null)
                Debug.LogError("[ValveDisplay] FlotationPlantModel не найден!");
        }

        _prevValve = _model != null ? _model.u_ValveOut : 0f;
    }

    void Update()
    {
        if (_model == null) return;

        float valve = _model.u_ValveOut;

        UpdateState(valve);
        ApplyVisuals();

        _prevValve = valve;
    }

    // =========================================================================
    void UpdateState(float valve)
    {
        bool isOpen = valve > openThreshold;

        switch (_currentState)
        {
            case ValveState.Closed:
                if (isOpen) { _currentState = ValveState.StartingOpen; _transTimer = 0f; }
                break;

            case ValveState.StartingOpen:
                _transTimer += Time.deltaTime;
                if (!isOpen) { _currentState = ValveState.Stopped; break; }
                if (_transTimer >= transitionTime) _currentState = ValveState.Opening;
                break;

            case ValveState.Opening:
                if (!isOpen) { _currentState = ValveState.StartingClose; _transTimer = 0f; break; }
                if (valve >= 0.99f) _currentState = ValveState.Open;
                break;

            case ValveState.Open:
                if (!isOpen) { _currentState = ValveState.StartingClose; _transTimer = 0f; }
                break;

            case ValveState.StartingClose:
                _transTimer += Time.deltaTime;
                if (isOpen) { _currentState = ValveState.Open; break; }
                if (_transTimer >= transitionTime) _currentState = ValveState.Closing;
                break;

            case ValveState.Closing:
                if (isOpen) { _currentState = ValveState.Opening; break; }
                if (valve <= 0f) _currentState = ValveState.Closed;
                break;

            case ValveState.Stopped:
                if (isOpen) _currentState = ValveState.Opening;
                else if (valve <= 0) _currentState = ValveState.Closed;
                break;
        }
    }

    // =========================================================================
    void ApplyVisuals()
    {
        Color lCol; float lHz;
        Color rCol; float rHz;

        switch (_currentState)
        {
            case ValveState.Closed:
                lCol = ColGreen; lHz = 0; rCol = ColGreen; rHz = 0; break;
            case ValveState.StartingOpen:
                lCol = ColRed; lHz = 0.5f; rCol = ColGreen; rHz = 0; break;
            case ValveState.Opening:
                lCol = ColRed; lHz = 2f; rCol = ColGreen; rHz = 0; break;
            case ValveState.Open:
                lCol = ColRed; lHz = 0; rCol = ColRed; rHz = 0; break;
            case ValveState.StartingClose:
                lCol = ColRed; lHz = 0; rCol = ColGreen; rHz = 0.5f; break;
            case ValveState.Closing:
                lCol = ColRed; lHz = 0; rCol = ColGreen; rHz = 2f; break;
            case ValveState.Stopped:
                lCol = ColRed; lHz = 0; rCol = ColGreen; rHz = 0; break;
            default: // Unreliable
                lCol = ColWhite; lHz = 0.5f; rCol = ColWhite; rHz = 0.5f; break;
        }

        _blinkTimer += Time.deltaTime;

        SetSVG(_leftTriangle, lCol, lHz);
        SetSVG(_rightTriangle, rCol, rHz);
    }

    void SetSVG(SVGImage img, Color col, float hz)
    {
        if (img == null) return;

        if (hz <= 0f)
        {
            img.color = col;
        }
        else
        {
            float period = 1f / hz;
            bool visible = ((_blinkTimer % period) < period * 0.5f);
            img.color = visible ? col : Color.clear;
        }
    }
}