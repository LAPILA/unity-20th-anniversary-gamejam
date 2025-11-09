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

    [Header("Boss Eyes")]
    [Tooltip("보스의 왼쪽, 오른쪽 눈 MeshRenderer를 연결하세요.")]
    public MeshRenderer[] bossEyes;   // 눈 두짝 연결
    public Color idleColor = Color.white;
    public Color firingColor = Color.red;

    private bool _isLaserActive = false;
    private bool _isFiringCoroutineRunning = false;
    private bool _isEnabled = false;
    private float _timer = 0f;

    void Start()
    {
        if (laserLineRenderer != null)
            laserLineRenderer.enabled = false;

        _timer = 0f;

        // 초기 눈 색상 설정
        SetEyeColor(idleColor);
    }

    void Update()
    {
        if (!_isEnabled) return;

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
            PlayerController pc = hitObject.GetComponentInParent<PlayerController>();
            Debug.Log($"플레이어 적중! 즉사 처리 예정. 오브젝트: {hitObject.name}");
            pc.Die();
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

        if (isActive)
        {
            SetEyeColor(firingColor); // 빨갛게
        }
        else
        {
            SetEyeColor(idleColor);   // 원래 색으로 복귀
            if (laserLineRenderer != null)
                laserLineRenderer.enabled = false;
        }
    }

    private void SetEyeColor(Color color)
    {
        if (bossEyes == null || bossEyes.Length == 0) return;

        foreach (var eye in bossEyes)
        {
            if (eye != null && eye.material != null)
            {
                eye.material.color = color;
            }
        }
    }

    // 외부에서 호출 (예: 트리거 존)
    public void Activate()
    {
        _isEnabled = true;
        _timer = 0f;
        Debug.Log("보스 레이저 시스템 활성화됨!");
    }
}
