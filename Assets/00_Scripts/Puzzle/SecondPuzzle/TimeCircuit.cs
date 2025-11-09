using UnityEngine;
using System;

[RequireComponent(typeof(Renderer))]
public class TimeCircuit : MonoBehaviour, ITimeActivatable
{
    [Header("Circuit Settings")]
    public string circuitId = "A";
    [SerializeField] private Renderer circuitRenderer;
    public Color stoppingColor = Color.red;
    public Color flowingColor = Color.green;

    [Header("Auto Blink Settings")]
    public bool useAutoBlink = false;
    public float blinkPeriod = 4f;
    [Range(0f, 1f)] public float onRatio = 0.5f;
    public float phaseOffset = 0f;

    public event Action<TimeCircuit, bool> OnCircuitStateChanged;

    private bool _isRequestingStop = true;
    private Material _material;
    private float _timer;

    public bool IsFlowing => !_isRequestingStop;

    void Awake()
    {
        if (circuitRenderer == null)
            circuitRenderer = GetComponent<Renderer>();
        if (circuitRenderer != null)
            _material = circuitRenderer.material;

        UpdateVisuals();
    }

    void Update()
    {
        if (!useAutoBlink) return;

        _timer += Time.deltaTime;
        float localTime = (_timer + phaseOffset) % blinkPeriod;
        float onDuration = blinkPeriod * onRatio;

        bool shouldFlow = localTime < onDuration;

        if (shouldFlow != !_isRequestingStop)
        {
            _isRequestingStop = !shouldFlow;
            UpdateVisuals();
            OnCircuitStateChanged?.Invoke(this, IsFlowing);
        }
    }

    public void ToggleTimeState()
    {
        //클릭 순간에는 색을 바꾸지 않고, 단순히 AutoBlink 토글만 수행
        useAutoBlink = !useAutoBlink;

        Debug.Log($"{circuitId} Circuit AutoBlink {(useAutoBlink ? "Enabled" : "Disabled")}");

        //깜빡임이 꺼질 때 마지막 상태 유지하도록 (이벤트 알림)
        if (!useAutoBlink)
        {
            OnCircuitStateChanged?.Invoke(this, IsFlowing);
        }
    }

    private void UpdateVisuals()
    {
        if (_material == null) return;

        Color current = _isRequestingStop ? stoppingColor : flowingColor;
        _material.color = current;
        _material.SetColor("_EmissionColor", current * (_isRequestingStop ? 0.5f : 1.5f));
    }
}
