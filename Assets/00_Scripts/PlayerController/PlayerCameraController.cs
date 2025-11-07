using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using Sirenix.OdinInspector;

/// <summary>
/// 1인칭 카메라 컨트롤러 (Cinemachine 3 Virtual Camera)
/// ▸ 마우스 회전 / 줌 / 헤드밥 / 카메라 높이 보간(앉기) / FOV 연출
/// ▸ 슬라이드 연출/틸트/추가 파라미터 전부 제거
/// ▸ 최적화: LateUpdate에서 오프셋+헤드밥 최종 1회 적용, 불필요한 변환 최소화
/// </summary>
[DisallowMultipleComponent]
public class PlayerCameraController : SerializedMonoBehaviour
{
    // ────────────────────────────────────────────────────────────
    #region ▸ Camera
    [BoxGroup("Camera"), Required] public CinemachineCamera cam;
    [BoxGroup("Camera"), Range(20, 120)] public float baseFOV = 66f;
    [BoxGroup("Camera")] public bool invertY = false;
    [BoxGroup("Camera"), Range(.1f, 10f)] public float sens = 1f;
    [BoxGroup("Camera"), Range(20, 89)] public float maxPitch = 50f;
    [BoxGroup("Camera")] public bool smooth = false;
    [BoxGroup("Camera"), Range(0, 20f)] public float smoothT = 10f;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Offsets (Crouch)
    [BoxGroup("Offsets"), Tooltip("앉기 시 카메라 로컬 Y 오프셋")]
    public float crouchOffsetY = -0.5f;
    [BoxGroup("Offsets"), Tooltip("오프셋 보간 속도")]
    public float offsetLerp = 10f;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Zoom / FOV
    [BoxGroup("Zoom")] public bool enableZoom = true;
    [BoxGroup("Zoom")] public bool holdToZoom = false;
    [BoxGroup("Zoom"), Range(10, 120)] public float zoomFOV = 30f;
    [BoxGroup("Zoom"), Range(.1f, 20)] public float zoomLerpSpd = 8f;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Crosshair (선택)
    [BoxGroup("Crosshair")] public bool showCross = true;
    [BoxGroup("Crosshair")] public Sprite crossSprite;
    [BoxGroup("Crosshair")] public Color crossColor = Color.white;
    [BoxGroup("Crosshair"), Required] public Image crossImage;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Internal
    float _yawRaw, _pitchRaw, _yawSm, _pitchSm;
    bool _zoomed, _zoomHeld, _isSliding;

    [BoxGroup("Head"), Required, Tooltip("헤드밥/오프셋 Pivot")]
    public Transform joint;

    [BoxGroup("Head"), Tooltip("헤드밥 좌우 움직임 강도")] public float bobAmountX = 0.06f;
    [BoxGroup("Head"), Tooltip("헤드밥 상하 움직임 강도")] public float bobAmountY = 0.045f;
    [BoxGroup("Head"), Tooltip("헤드밥 속도")] public float bobFrequency = 10f;
    [BoxGroup("Head"), Tooltip("달리기 시 헤드밥 속도 배율")] public float sprintBobMultiplier = 1.2f;

    Vector3 _jointOrigin;

    float _targetYOffset = 0f;  // 외부(Player)에서 지정
    float _currentYOffset = 0f; // 보간 결과

    public bool cameraCanMove = true;

    PlayerController _player; // 플레이어 상태 참조(IsWalking)
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Unity
    void Awake()
    {
        _player = GetComponentInParent<PlayerController>();

        if (cam) cam.Lens.FieldOfView = baseFOV;
        if (joint) _jointOrigin = joint.localPosition;

        // 자식 스케일 고정(시각 오브젝트 줄어듦 방지)
        transform.localScale = Vector3.one;
        joint.localScale = Vector3.one;
    }

    void Start()
    {
        // 크로스헤어 초기화
        if (crossImage)
        {
            crossImage.gameObject.SetActive(showCross);
            if (showCross)
            {
                crossImage.sprite = crossSprite;
                crossImage.color = crossColor;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!cameraCanMove) return;

        HandleRotation();
        if (enableZoom) UpdateZoomState();
    }

    /// <summary>
    /// Cinemachine이 LateUpdate에서 포즈를 갱신하므로,
    /// 오프셋/헤드밥은 LateUpdate에서 최종 1회 적용.
    /// </summary>
    void LateUpdate()
    {
        if (!cameraCanMove) return;

        _currentYOffset = Mathf.Lerp(_currentYOffset, _targetYOffset, Time.deltaTime * offsetLerp);

        Vector3 bob = Vector3.zero;
        if (_player && (_player.IsWalking || _player.IsSprinting))
        {
            float currentBobFrequency = _player.IsSprinting ? bobFrequency * sprintBobMultiplier : bobFrequency;
            bob = new Vector3(
                Mathf.Sin(Time.time * currentBobFrequency) * bobAmountX,       // 좌우
                Mathf.Sin(Time.time * currentBobFrequency * 2f) * bobAmountY,  // 상하
                0f
            );
        }

        // 3) 최종 위치 1회 적용
        joint.localPosition = _jointOrigin + new Vector3(0, _currentYOffset, 0) + bob;
    }
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Input Callbacks
    public void OnLookInput(Vector2 delta)
    {
        _yawRaw += delta.x * sens;
        _pitchRaw += (invertY ? delta.y : -delta.y) * sens;
        _pitchRaw = Mathf.Clamp(_pitchRaw, -maxPitch, maxPitch);
    }

    public void OnZoomPerformed()
    {
        if (!enableZoom) return;
        _zoomHeld = true;
        if (!holdToZoom) _zoomed = !_zoomed;
    }

    public void OnZoomCanceled()
    {
        if (!enableZoom) return;
        _zoomHeld = false;
    }
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Rotation
    void HandleRotation()
    {
        if (smooth)
        {
            _yawSm = Mathf.Lerp(_yawSm, _yawRaw, Time.deltaTime * smoothT);
            _pitchSm = Mathf.Lerp(_pitchSm, _pitchRaw, Time.deltaTime * smoothT);
        }
        else
        {
            _yawSm = _yawRaw;
            _pitchSm = _pitchRaw;
        }

        if (_player)
        {
            _player.transform.localEulerAngles = new Vector3(0f, _yawSm, 0f);
        }

        if (cam)
        {
            cam.transform.localEulerAngles = new Vector3(_pitchSm, 0f, 0f);
        }
    }
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Offsets / FOV
    /// <summary>Player에서 호출: 목표 오프셋(Y) 지정</summary>
    public void SetCrouchOffset(float targetY) => _targetYOffset = targetY;

    /// <summary>스프린트/줌/슬라이드 상태별 FOV</summary>
    public void SyncFOV(bool sprinting, float sprintFov, float lerpSpeed)
    {
        float target = baseFOV;
        if (_zoomed) target = zoomFOV;
        else if (_isSliding) target = sprintFov;
        else if (sprinting) target = sprintFov;

        float cur = cam.Lens.FieldOfView;
        float nf = Mathf.Lerp(cur, target, lerpSpeed * Time.deltaTime);
        if (Mathf.Abs(nf - cur) > 0.0001f) cam.Lens.FieldOfView = nf;
    }

    /// <summary>PlayerController에서 슬라이딩 상태를 전달</summary>
    public void SetSlideState(bool isSliding, float targetFov, float lerpSpeed)
    {
        _isSliding = isSliding;
        // 슬라이딩 전용 FOV 로직은 SyncFOV에서 처리
    }

    void UpdateZoomState()
    {
        if (holdToZoom) _zoomed = _zoomHeld;
    }
    #endregion
}
