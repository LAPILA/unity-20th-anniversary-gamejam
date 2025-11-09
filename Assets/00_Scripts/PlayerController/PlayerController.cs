using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

/// <summary>
/// 1인칭 플레이어 컨트롤러 (Unity 6 / New Input System / CM3)
/// ▸ 이동 / 달리기 / 점프(버퍼) / 앉기
/// ▸ 커스텀 중력, 발소리(FEEL), 카메라 연동(오프셋·FOV·줌)
/// ▸ 최적화: Ground 체크는 Fixed 전용, 마스크/치수 캐싱, 불필요 수정 최소화
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : SerializedMonoBehaviour
{
    // ────────────────────────────────────────────────────────────
    #region ▸ References / Input
    [BoxGroup("Input"), Tooltip("IA_Player (New Input System)"), Required] public PlayerInput input;
    [BoxGroup("References"), Required] public PlayerCameraController cam;
    [BoxGroup("References"), Required] public CapsuleCollider capsule;
    [BoxGroup("References"), Required] public PlayerFeedbacks fx;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Move / Jump
    [BoxGroup("Move")] public float walkSpeed = 5f;
    [BoxGroup("Move")] public float sprintSpeed = 10f;
    [BoxGroup("Move"), Tooltip("타깃 속도로의 변화량 상한")] public float maxVelChange = 10f;

    [BoxGroup("Jump")] public bool enableJump = true;
    [BoxGroup("Jump")] public bool holdToJump = false; // 필요시 확장
    [BoxGroup("Jump"), Range(1f, 1500f)] public float jumpPower = 600f;
    [BoxGroup("Jump"), Tooltip("점프 입력 버퍼(초)")] public float jumpBufferTime = 0.12f;
    private float _jumpBufferCounter;
    private bool _jumpDownThisFrame;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Crouch
    [BoxGroup("Crouch")] public bool holdToCrouch = true;
    [BoxGroup("Crouch"), Range(.3f, 1f)] public float crouchHeight = .65f;
    [BoxGroup("Crouch"), Range(.2f, 1f)] public float crouchSpeedMul = .45f;
    private bool _crouched;
    private bool _crouchHeld, _crouchPressedThisFrame;

    [BoxGroup("Slide")] public bool enableSliding = true;
    [BoxGroup("Slide"), Tooltip("슬라이딩 지속 시간(초)")] public float slideDuration = 0.6f;
    [BoxGroup("Slide"), Tooltip("슬라이딩 시 순간적으로 추가되는 속도")] public float slideSpeedBoost = 5f;
    private bool _isSliding;
    private float _slideTimer;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Camera / FOV
    [BoxGroup("FOV")] public float sprintFOV = 80f;
    [BoxGroup("FOV")] public float fovLerp = 10f;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Footstep
    [BoxGroup("Footstep")] public float footstepIntervalWalk = 0.45f;
    [BoxGroup("Footstep")] public float footstepIntervalRun = 0.3f;
    private float _footstepTimer;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Physics / Ground
    [BoxGroup("Physics"), Tooltip("Ground 체크 거리")] public float groundCheckDist = 0.2f;
    [BoxGroup("Physics"), Tooltip("바닥 레이어")] public LayerMask groundMask = ~0;
    [BoxGroup("Physics"), Tooltip("허용 경사(도)")] public float maxGroundAngle = 55f;

    [BoxGroup("Physics"), Tooltip("경사면 미끄러짐 가속도")] public float slopeSlideGravity = 1.2f;
    private bool _onSteepSlope;
    // 벽 처리용(추가)
    [BoxGroup("Physics"), Tooltip("벽 판정을 위한 캐스트 거리")] public float wallCheckDistance = 0.6f;
    [BoxGroup("Physics"), Tooltip("벽 밀어내기 강도 (작은 값 추천)")] public float wallRepelForce = 0.12f;

    private bool _grounded, _wasGrounded;
    private bool _groundedFixed;      // FixedUpdate에서 계산한 값
    private bool _justLandedFixed;    // Fixed에서 착지 감지
    private Vector3 _groundNormal = Vector3.up;
    private int _groundMaskCached;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ Capsule Base Cache (정확 복원)
    private float _capBaseH;
    private Vector3 _capBaseCenter;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▸ State
    private Rigidbody _rb;
    private IA_Player _actions;
    private Vector2 _moveInput;
    private bool _sprinting, _sprintHeld;
    private float _moveSpeed;
    private bool _canMove = true;

    // R/O
    public bool IsSprinting => _sprinting;
    public bool IsCrouched => _crouched;
    public bool IsWalking { get; private set; }

    public Vector2 CurrentMoveInput => _moveInput;
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Unity
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.None;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _groundMaskCached = groundMask.value;
        if (!capsule) capsule = GetComponent<CapsuleCollider>();

        // 캡슐 원본 치수 캐시(앉기/복원 정확성)
        _capBaseH = capsule.height;
        _capBaseCenter = capsule.center;

        if (_actions == null) _actions = new IA_Player();
    }

    void OnEnable()
    {
        _actions.Player.Enable();

        _actions.Player.Move.performed += c => _moveInput = c.ReadValue<Vector2>();
        _actions.Player.Move.canceled += _ => _moveInput = Vector2.zero;

        _actions.Player.Look.performed += c => cam.OnLookInput(c.ReadValue<Vector2>());
        _actions.Player.Look.canceled += _ => cam.OnLookInput(Vector2.zero);

        _actions.Player.Sprint.performed += _ => _sprintHeld = true;
        _actions.Player.Sprint.canceled += _ => _sprintHeld = false;

        _actions.Player.Crouch.performed += _ =>
        {
            _crouchPressedThisFrame = true;
            if (holdToCrouch) _crouchHeld = true; else _crouchHeld = !_crouchHeld;
        };
        _actions.Player.Crouch.canceled += _ => { if (holdToCrouch) _crouchHeld = false; };

        _actions.Player.Jump.performed += _ => _jumpDownThisFrame = true;

        _actions.Player.Zoom.performed += _ => cam.OnZoomPerformed();
        _actions.Player.Zoom.canceled += _ => cam.OnZoomCanceled();
    }

    void OnDisable() => _actions?.Player.Disable();

    void Update()
    {
        if (!_canMove) return;

        // Ground는 Fixed에서 계산 → 여기서는 캐시만 사용
        _grounded = _groundedFixed;

        HandleSprint();
        HandleSlide();
        HandleJumpBuffered();
        HandleCrouch();

        // "걷기" 판정(앉는 중 제외)
        IsWalking = _grounded && _moveInput.sqrMagnitude > 0.01f && !_crouched && !_isSliding;

        HandleFootsteps();
        cam?.SyncFOV(_sprinting, sprintFOV, fovLerp);

        // 착지 이벤트(연출 시점)
        if (_justLandedFixed)
        {
            fx?.Land();
            _justLandedFixed = false;
        }

        // 프레임 플래그 초기화
        _crouchPressedThisFrame = false;
        _jumpDownThisFrame = false;
        _wasGrounded = _grounded;
    }

    void FixedUpdate()
    {
        if (!_canMove) return;

        // Ground 체크: 물리 프레임에서만 수행
        bool prevGround = _groundedFixed;
        _groundedFixed = CheckGround();
        _justLandedFixed = (!prevGround && _groundedFixed);

        HandleSlopeSlide();

        MoveCharacter();
    }
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Movement
    /// <summary>수평 가속만 제어해서 타이트한 FPS 감각 유지</summary>
    void MoveCharacter()
    {
        if (_isSliding || _onSteepSlope) return;

        Vector3 dir = new Vector3(_moveInput.x, 0, _moveInput.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float speed = _moveSpeed * (_crouched ? crouchSpeedMul : 1f);

        // 방향(월드) 계산
        Vector3 desiredDir = transform.TransformDirection(dir);
        Vector3 targetVel = desiredDir * speed;

        // --- 벽 판정: 앞으로 짧게 SphereCast해서 법선이 있으면 이동을 그 평면으로 프로젝션 ---
        if (dir.sqrMagnitude > 0.0001f)
        {
            // 캐릭터 상중심에서 체크 (머리 높이·중간 높이 등으로 조절 가능)
            float originHeight = Mathf.Clamp(capsule.height * 0.5f, 0.1f, 2f);
            Vector3 origin = transform.position + Vector3.up * originHeight;

            float radius = Mathf.Max(0.01f, capsule.radius * 0.9f);
            RaycastHit hit;
            if (Physics.SphereCast(origin, radius, transform.TransformDirection(dir), out hit, wallCheckDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                // 수직 성분이 작으면 벽으로 판단 (값은 필요시 튜닝)
                if (Mathf.Abs(hit.normal.y) < 0.7f)
                {
                    // 이동을 벽의 평면으로 투영 -> 벽에 붙지 않고 슬라이드
                    targetVel = Vector3.ProjectOnPlane(targetVel, hit.normal);

                    // 약간 벽에서 밀어내는 보정 (작은 값 권장)
                    _rb.AddForce(hit.normal * wallRepelForce, ForceMode.VelocityChange);
                }
            }
        }

        Vector3 hVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 velChange = Vector3.ClampMagnitude(targetVel - hVel, maxVelChange);

        if (velChange.sqrMagnitude > 0.000001f)
            _rb.AddForce(new Vector3(velChange.x, 0f, velChange.z), ForceMode.VelocityChange);
    }

    /// <summary>가파른 경사면에서 미끄러지는 물리 처리 (FixedUpdate)</summary>
    void HandleSlopeSlide()
    {
        // 가파른 경사면에 있고, (앉기)슬라이딩 중이 아닐 때
        if (_onSteepSlope && !_isSliding)
        {
            // 중력에 slopeSlideGravity 배율을 적용
            Vector3 gravity = Physics.gravity * slopeSlideGravity;

            // 중력을 경사면 법선에 투영하여 미끄러지는 방향 벡터를 얻음
            Vector3 slideDirection = Vector3.ProjectOnPlane(gravity, _groundNormal);

            // 미끄러지는 힘 적용 (가속도 모드: 질량과 무관하게 일정하게 가속)
            _rb.AddForce(slideDirection, ForceMode.Acceleration);
        }
    }
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Jump (버퍼)
    void HandleJumpBuffered()
    {
        if (!enableJump) { _jumpBufferCounter = 0f; return; }
        if (_jumpDownThisFrame) _jumpBufferCounter = jumpBufferTime;

        // 지상에서만 점프 처리
        if (_jumpBufferCounter > 0f && _grounded)
        {
            // 수직 속도 초기화 후 점프 임펄스
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);

            fx?.Jump();
            _jumpBufferCounter = 0f;
            _grounded = false;
            _groundedFixed = false;
        }
        _jumpBufferCounter -= Time.deltaTime;
    }
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Crouch
    /// <summary>지상에서만 상태 변경. 공중에서는 시각효과 없음</summary>
    void HandleCrouch()
    {
        if (enableSliding && _crouchPressedThisFrame && _sprinting && _grounded)
        {
            StartSlide();
            return;
        }

        if (!_grounded || _isSliding) return;

        if (_crouchPressedThisFrame && !holdToCrouch)
        {
            if (_crouched) Stand();
            else Crouch();
        }

        if (holdToCrouch)
        {
            if (_crouchHeld && !_crouched) Crouch();
            else if (!_crouchHeld && _crouched) TryStand();
        }
    }

    void Crouch()
    {
        _crouched = true;
        ApplyCapsuleHeight(crouchHeight);
        cam.SetCrouchOffset(cam.crouchOffsetY);
        // fx?.CrouchStart?.Invoke();
    }

    void Stand()
    {
        if (!CanStandUp()) return;
        _crouched = false;
        ApplyCapsuleHeight(1f);
        cam.SetCrouchOffset(0f);
        // fx?.CrouchEnd?.Invoke();
    }

    void TryStand() { if (CanStandUp()) Stand(); }

    /// <summary>머리 위 여유 공간 확인 (스피어캐스트)</summary>
    bool CanStandUp()
    {
        float r = capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float h = Mathf.Max(capsule.height * transform.lossyScale.y, r * 2f);
        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 top = center + Vector3.up * (h * 0.5f - r);
        return !Physics.SphereCast(top, r * 0.95f, Vector3.up, out _, 0.25f, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>스케일 변경 없이 '원본 × 비율'로 콜라이더만 조정</summary>
    void ApplyCapsuleHeight(float mul)
    {
        capsule.height = Mathf.Max(0.1f, _capBaseH) * mul;
        Vector3 c = _capBaseCenter;
        capsule.center = new Vector3(c.x, c.y * mul, c.z);
    }
    #endregion

    // ────────────────────────────────────────────────────────────

    #region ▶ Slide
    void HandleSlide()
    {
        if (!_isSliding) return;

        _slideTimer -= Time.deltaTime;

        // 슬라이딩 종료 조건 (시간 종료 또는 속도 감소)
        if (_slideTimer <= 0f || _rb.linearVelocity.magnitude < walkSpeed)
        {
            StopSlide();
        }
    }

    void StartSlide()
    {
        _isSliding = true;
        _slideTimer = slideDuration;

        // 슬라이딩 시작 시 즉시 앉기 상태 적용
        Crouch();

        // 진행 방향으로 순간 가속
        _rb.AddForce(transform.forward * slideSpeedBoost, ForceMode.VelocityChange);

        cam?.SetSlideState(true, sprintFOV, fovLerp); // 슬라이딩 시 FOV 변경
    }

    void StopSlide()
    {
        _isSliding = false;

        // 슬라이딩이 끝나면 앉은 상태를 유지. 카메라 효과도 원복.
        cam?.SetSlideState(false, cam.baseFOV, fovLerp);
    }
    #endregion

    #region ▶ Sprint
    void HandleSprint()
    {
        _sprinting = _sprintHeld && !_crouched && !_isSliding;
        _moveSpeed = _sprinting ? sprintSpeed : walkSpeed;
    }
    #endregion

    // ────────────────────────────────────────────────────────────
    #region ▶ Footstep
    /// <summary>속도/프레임 영향 없는 타이머 방식 발소리</summary>
    void HandleFootsteps()
    {
        if (!_grounded || _moveInput.sqrMagnitude <= .01f)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            fx?.Footstep();
            _footstepTimer = IsSprinting ? footstepIntervalRun : footstepIntervalWalk;
        }
    }
    #endregion

    // ───────────────────────────────────────────────────────────-
    #region ▶ Ground Check (Fixed 전용)
    /// <summary>캡슐 하단 스피어캐스트로 지면/경사 판정</summary>
    bool CheckGround()
    {
        // ▼▼▼ [2] CheckGround() 함수 전체를 이 코드로 교체 ▼▼▼
        float r = Mathf.Max(0.01f, capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        float h = Mathf.Max(capsule.height * transform.lossyScale.y, r * 2f);

        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 bottom = center + Vector3.down * (h * 0.5f - r + 0.01f);

        // 캐스트 전 상태 초기화
        _onSteepSlope = false;

        bool hit = Physics.SphereCast(
            bottom + Vector3.up * 0.02f,
            r * 0.98f,
            Vector3.down,
            out RaycastHit info,
            groundCheckDist + 0.02f,
            _groundMaskCached,
            QueryTriggerInteraction.Ignore
        );

        if (hit)
        {
            _groundNormal = info.normal; // 법선 벡터 저장

            // 경사각이 허용치보다 큰 경우 (가파른 경사)
            if (Vector3.Angle(_groundNormal, Vector3.up) > maxGroundAngle)
            {
                _onSteepSlope = true; // 미끄러짐 상태 ON
                return false;         // "Grounded" (점프 가능) 상태는 아님
            }
            // 경사각이 허용치 이내인 경우 (걸을 수 있는 땅)
            return true;
        }

        _groundNormal = Vector3.up;
        return false;
    }
    #endregion

    // ───────────────────────────────────────────────────────────-
    #region ▶ External
    public void LockMovement(bool locked)
    {
        _canMove = !locked;
        if (cam) cam.cameraCanMove = !locked;
    }

    public void SetUIMode(bool ui)
    {
        LockMovement(ui);
        Cursor.lockState = ui ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = ui;

        if (ui)
        {
            _actions.Player.Disable();
        }
        else
        {
            _actions.Player.Enable();
        }
    }
    #endregion
}
