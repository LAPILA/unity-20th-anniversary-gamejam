using UnityEngine;
using System.Collections;

public class BossLaserController : MonoBehaviour
{
    [Header("Laser References")]
    public Transform laserOrigin;
    public LineRenderer laserLineRenderer;

    [Header("Laser Settings")]
    public float laserMaxDistance = 100f;
    public LayerMask playerLayer;
    public LayerMask batteryBoxLayer;

    [Header("자동 발사 설정")]
    public float fireInterval = 3f;   // 발사 주기
    public float fireDuration = 1f;   // 발사 유지 시간

    private bool _isLaserActive = false;
    private bool _isFiringCoroutineRunning = false;
    private bool _isEnabled = false;  // 플레이어가 구역 진입하기 전엔 작동 안함
    private float _timer = 0f;

    void Start()
    {
        if (laserLineRenderer != null)
            laserLineRenderer.enabled = false;

        _timer = 0f;
    }

    void Update()
    {
        if (!_isEnabled) return;  // 🔹 아직 활성화되지 않았으면 아무것도 안함

        if (!_isLaserActive && !_isFiringCoroutineRunning)
        {
            _timer += Time.deltaTime;

            if (_timer >= fireInterval)
            {
                _timer = 0f;
                StartCoroutine(FireLaserForDuration());
            }
        }

        if (_isLaserActive)
            FireLaser();
    }

    private IEnumerator FireLaserForDuration()
    {
        _isFiringCoroutineRunning = true;
        SetLaserActive(true);
        yield return new WaitForSeconds(fireDuration);
        SetLaserActive(false);
        _isFiringCoroutineRunning = false;
    }

    public void FireLaser()
    {
        if (laserOrigin == null || laserLineRenderer == null) return;

        Vector3 origin = laserOrigin.position;
        Vector3 direction = laserOrigin.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, laserMaxDistance))
        {
            SetLaserEndPoint(hit.point);
            HandleHit(hit.collider.gameObject);
        }
        else
        {
            SetLaserEndPoint(origin + direction * laserMaxDistance);
        }

        laserLineRenderer.enabled = true;
    }

    private void HandleHit(GameObject hitObject)
    {
        int layer = hitObject.layer;

        if (((1 << layer) & playerLayer) != 0)
        {
            Debug.Log($"플레이어 적중! 즉사 처리 예정. 오브젝트: {hitObject.name}");
            PlayerController pc = hitObject.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.RespawnAtCheckpoint();
            }
        }
        else if (((1 << layer) & batteryBoxLayer) != 0)
        {
            BatteryBox batteryBox = hitObject.GetComponent<BatteryBox>();
            if (batteryBox != null)
            {
                batteryBox.ChargeBattery();
                Debug.Log($"배터리 박스 충전됨: {hitObject.name}");
            }
        }
    }

    private void SetLaserEndPoint(Vector3 endPoint)
    {
        laserLineRenderer.SetPosition(0, laserOrigin.position);
        laserLineRenderer.SetPosition(1, endPoint);
    }

    public void SetLaserActive(bool isActive)
    {
        _isLaserActive = isActive;
        if (!isActive && laserLineRenderer != null)
            laserLineRenderer.enabled = false;
    }

    // 외부에서 호출 (예: 트리거 존)
    public void Activate()
    {
        _isEnabled = true;
        _timer = 0f;
        Debug.Log("보스 레이저 시스템 활성화됨!");
    }
}
