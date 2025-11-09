using UnityEngine;
using System.Collections.Generic;

public class BatteryManager : MonoBehaviour
{
    [Header("Manager Settings")]
    [Tooltip("씬에서 필요한 총 배터리 박스의 개수")]
    public int requiredBatteries = 5;

    [Tooltip("모든 배터리가 충전되었는지 여부 (읽기 전용)")]
    [SerializeField] private bool _allCharged = false;

    // 현재 충전이 완료된 배터리 박스 목록
    private List<BatteryBox> _chargedBatteries = new List<BatteryBox>();

    // 씬에 있는 모든 BatteryBox 인스턴스
    private BatteryBox[] _allBatteryBoxes;

    void Awake()
    {
        // 씬 로드 시 모든 BatteryBox 컴포넌트를 찾습니다.
        _allBatteryBoxes = FindObjectsOfType<BatteryBox>();

        // 찾은 개수가 필요한 개수와 다르면 경고
        if (_allBatteryBoxes.Length != requiredBatteries)
        {
            Debug.LogWarning($"[BatteryManager] 씬에서 {requiredBatteries}개가 필요하지만, {_allBatteryBoxes.Length}개만 발견되었습니다.");
        }
    }

    void OnEnable()
    {
        BatteryBox.OnChargeStatusChanged += OnBatteryStatusChanged;
    }

    void OnDisable()
    {
        BatteryBox.OnChargeStatusChanged -= OnBatteryStatusChanged;
    }

    private void OnBatteryStatusChanged(BatteryBox battery, bool isCharged)
    {
        if (isCharged)
        {
            if (!_chargedBatteries.Contains(battery))
            {
                _chargedBatteries.Add(battery);
            }

            // 모든 배터리가 충전되었는지 확인
            if (_chargedBatteries.Count >= requiredBatteries && !_allCharged)
            {
                _allCharged = true;
                AllBatteriesCharged();
            }
        }
    }

    private void AllBatteriesCharged()
    {

        Debug.LogError("Launch!");
    }
}