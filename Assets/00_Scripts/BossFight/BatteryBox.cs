using UnityEngine;
using System;

public class BatteryBox : MonoBehaviour
{
    [Header("Charge Settings")]
    public float maxCharge = 100f;
    [Range(0, 100)] public float currentCharge = 0f;

    [Header("Feedback Settings")]
    public Color unchargedColor = Color.red;
    public Color chargedColor = Color.green;

    public static event Action<BatteryBox, bool> OnChargeStatusChanged;

    private bool _isCharged = false;
    private Material _material;

    public bool IsCharged => _isCharged;
    private BossLaserController bossLaser;

    void Awake()
    {
        bossLaser = FindObjectOfType<BossLaserController>();

        // 자동으로 MeshRenderer나 SpriteRenderer 탐색
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            _material = renderer.material;
        }
        else
        {
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
                _material = sprite.material;
        }

        UpdateVisualFeedback();
    }

    public void ChargeBattery()
    {
        if (_isCharged) return;
        currentCharge = maxCharge;
        _isCharged = true;

        Debug.Log($"{gameObject.name}: 충전 완료!");
        OnChargeStatusChanged?.Invoke(this, true);
        UpdateVisualFeedback();
    }

    private void UpdateVisualFeedback()
    {
        if (_material == null) return;

        Color targetColor = _isCharged ? chargedColor : unchargedColor;
        _material.color = targetColor;
        _material.SetColor("_EmissionColor", targetColor * 1.5f);
    }
}
