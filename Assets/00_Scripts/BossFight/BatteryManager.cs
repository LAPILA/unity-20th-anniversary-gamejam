using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BatteryManager : MonoBehaviour
{
    [Header("Manager Settings")]
    [Tooltip("씬에서 필요한 총 배터리 박스의 개수")]
    public int requiredBatteries = 5;

    [Tooltip("모든 배터리가 충전되었는지 여부 (읽기 전용)")]
    public bool _allCharged = false;

    [Header("Door Destruction Settings")]
    [Tooltip("파괴할 문짝 GameObjects 3개를 연결하세요.")]
    public GameObject[] doorObjects;

    [Tooltip("문짝 파괴 시 활성화할 객체 (예: 최종 보스, 다음 레벨 포털 등)")]
    public GameObject objectToActivate;

    [Header("Optional Destruction Feedback")]
    [Tooltip("문짝 파괴 시 재생할 파티클 시스템(폭발 이펙트) 프리팹")]
    public GameObject destructionEffectPrefab;

    [Header("Cannon Animation (Legacy)")]
    [Tooltip("Animation 컴포넌트를 연결하세요.")]
    public Animation cannonAnimation;

    [Tooltip("재생할 애니메이션 클립")]
    public AnimationClip cannonFireClip; // 수동으로 실행할 애니메이션 클립

    public float explosionDelay = 1.0f; // 애니메이션 후 폭발까지 딜레이
    public float explosionForce = 800f;
    public float explosionRadius = 6f;

    private bool _hasFired = false;

    private List<BatteryBox> _chargedBatteries = new List<BatteryBox>();
    private BatteryBox[] _allBatteryBoxes;

    // 🔹 보스 레이저 참조
    private BossLaserController bossLaser;

    void Awake()
    {
        _allBatteryBoxes = FindObjectsOfType<BatteryBox>();
        bossLaser = FindObjectOfType<BossLaserController>();

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

        // 🔹 1. 보스 레이저 정지
        StopBossLaser();

        // 🔹 2. 문짝 파괴
        DestroyDoors();

        // 🔹 3. 다음 오브젝트 활성화
        ActivateTargetObject();
    }

    private void StopBossLaser()
    {
        if (bossLaser != null)
        {
            bossLaser.SetLaserActive(false);  // 즉시 레이저 끄기
            bossLaser.enabled = false;        // 아예 스크립트 비활성화 (주기적 발사 중지)
            Debug.Log("⚡ 보스 레이저 완전히 정지됨!");
        }
        else
        {
            Debug.LogWarning("BossLaserController를 찾을 수 없습니다.");
        }
    }

    private void DestroyDoors()
    {
        if (_hasFired) return;
        _hasFired = true;

        if (cannonAnimation != null && cannonFireClip != null)
        {
            cannonAnimation.Play(cannonFireClip.name);
            StartCoroutine(WaitAndExplodeDoors());
        }
        else
        {
            Debug.LogWarning("Animation 또는 클립이 설정되지 않았습니다. 바로 폭발 실행.");
            StartCoroutine(ExplodeDoorsPhysics());
        }
    }

    private void ActivateTargetObject()
    {
        if (objectToActivate != null)
        {
            objectToActivate.SetActive(true);
            Debug.Log($"객체 '{objectToActivate.name}'가 활성화되었습니다.");
        }
        else
        {
            Debug.LogWarning("활성화할 객체가 설정되지 않았습니다.");
        }
    }

    private IEnumerator WaitAndExplodeDoors()
    {
        yield return new WaitForSeconds(explosionDelay);
        StartCoroutine(ExplodeDoorsPhysics());
    }

    private IEnumerator ExplodeDoorsPhysics()
    {
        if (doorObjects == null || doorObjects.Length == 0)
        {
            Debug.LogWarning("파괴할 문짝 객체가 설정되지 않았습니다.");
            yield break;
        }

        foreach (GameObject door in doorObjects)
        {
            if (door == null) continue;

            if (destructionEffectPrefab != null)
                Instantiate(destructionEffectPrefab, door.transform.position, Quaternion.identity);

            Rigidbody rb = door.GetComponent<Rigidbody>();
            if (rb == null)
                rb = door.AddComponent<Rigidbody>();

            if (door.GetComponent<Collider>() == null)
                door.AddComponent<BoxCollider>();

            Vector3 explosionPos = transform.position;
            rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, 1f, ForceMode.Impulse);

            Destroy(door, 3f);
        }
        yield return null;
    }
}
