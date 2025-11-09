using UnityEngine;

public class CircuitActivator : MonoBehaviour, ITimeActivatable
{
    [Header("Circuit Activation")]
    [Tooltip("이 회로가 속한 그룹 컨트롤러 (세 회로를 묶음)")]
    public CircuitGroupController groupController;

    [Tooltip("회로 Identifier (디버깅용)")]
    public string circuitId = "Circuit A";

    [Header("Visual Settings")]
    [SerializeField] private Renderer circuitRenderer;
    public Color activeColor = Color.green;
    public Color inactiveColor = Color.red;

    private bool _isActive = false;
    private Material _material;

    public bool IsActive => _isActive; // 외부에서 상태 확인 가능

    void Awake()
    {
        if (circuitRenderer == null)
            circuitRenderer = GetComponent<Renderer>();

        if (circuitRenderer != null)
            _material = circuitRenderer.material;

        _isActive = false;
        UpdateVisuals();
    }

    public void ToggleTimeState()
    {
        _isActive = !_isActive;
        UpdateVisuals();

        Debug.Log($"{circuitId} 상태 토글: 활성화 = {_isActive}");

        // 그룹 컨트롤러에 상태 변경 알림
        groupController?.OnCircuitStateChanged();
    }

    private void UpdateVisuals()
    {
        if (_material != null)
        {
            _material.color = _isActive ? activeColor : inactiveColor;
            _material.SetColor("_EmissionColor", _material.color * (_isActive ? 1.5f : 0.5f));
        }
    }
}
