using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// PlayerController의 상태를 받아 3D 모델의 Animator를 관리합니다.
/// - 이동 블렌드 트리 (Idle, Run, Strafe)
/// - 회전 블렌드 트리 (Idle Turn)
/// - 이벤트 트리거 (Dying, Reviving)
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimatorManagement : MonoBehaviour
{
    [BoxGroup("References"), Required]
    [Tooltip("애니메이션을 재생할 3D 모델의 Animator 컴포넌트")]
    [SerializeField] private Animator modelAnimator;

    [BoxGroup("Settings")]
    [Tooltip("애니메이션 값이 변경될 때의 부드러움 (Damp Time)")]
    [SerializeField] private float smoothTime = 0.1f;

    [BoxGroup("Settings")]
    [Tooltip("TurnSpeed 파라미터 정규화를 위한 최대 회전 속도 (초당 각도)")]
    [SerializeField] private float maxTurnSpeed = 360f; // 1초에 360도 회전 시 1.0

    // 내부 참조
    private PlayerController _playerController;
    private float _lastYaw; // 이전 프레임의 Y축 회전값

    // 최적화를 위한 파라미터 해시
    private int _velXHash;
    private int _velZHash;
    private int _turnSpeedHash; // 회전 속도 파라미터
    private int _dieHash;
    private int _reviveHash;

    void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _lastYaw = transform.eulerAngles.y; // 현재 Y축 각도로 초기화

        // Animator 파라미터 이름을 ID(Hash)로 변환하여 캐시
        _velXHash = Animator.StringToHash("VelocityX");
        _velZHash = Animator.StringToHash("VelocityZ");
        _turnSpeedHash = Animator.StringToHash("TurnSpeed"); // "TurnSpeed" 파라미터 추가
        _dieHash = Animator.StringToHash("Die");
        _reviveHash = Animator.StringToHash("Revive");
    }

    void Update()
    {
        if (modelAnimator == null || _playerController == null) return;

        // --- 1. 이동 속도 전달 (VelocityX, VelocityZ) ---
        Vector2 moveInput = _playerController.CurrentMoveInput;
        bool isSprinting = _playerController.IsSprinting;

        // 달리기 상태일 때 Z값을 2로 증폭 (Blend Tree에서 1=걷기, 2=뛰기로 사용)
        float targetZ = moveInput.y * (isSprinting ? 2f : 1f);

        modelAnimator.SetFloat(_velXHash, moveInput.x, smoothTime, Time.deltaTime);
        modelAnimator.SetFloat(_velZHash, targetZ, smoothTime, Time.deltaTime);


        // --- 2. 회전 속도 계산 및 전달 (TurnSpeed) ---
        float currentYaw = transform.eulerAngles.y;

        // Mathf.DeltaAngle을 사용해 -180 ~ 180 범위의 정확한 각도 차이 계산
        float deltaYaw = Mathf.DeltaAngle(_lastYaw, currentYaw);

        // 프레임당 각도 변화량(deltaYaw)을 초당 각도 변화량(turnSpeed)으로 변환
        float turnSpeed = deltaYaw / Time.deltaTime;

        // -1.0 ~ 1.0 범위로 정규화
        float normalizedTurnSpeed = Mathf.Clamp(turnSpeed / maxTurnSpeed, -1f, 1f);

        // Animator에 부드럽게 전달
        modelAnimator.SetFloat(_turnSpeedHash, normalizedTurnSpeed, smoothTime, Time.deltaTime);

        // 다음 프레임 계산을 위해 현재 Yaw 저장
        _lastYaw = currentYaw;
    }

    /// <summary>
    /// 외부(예: PlayerHealth 스크립트)에서 호출하여 사망 애니메이션을 재생합니다.
    /// </summary>
    [Button("Test Die")]
    public void TriggerDie()
    {
        if (modelAnimator != null)
            modelAnimator.SetTrigger(_dieHash);
    }

    /// <summary>
    /// 외부(예: GameManager)에서 호출하여 부활 애니메이션을 재생합니다.
    /// </summary>
    [Button("Test Revive")]
    public void TriggerRevive()
    {
        if (modelAnimator != null)
            modelAnimator.SetTrigger(_reviveHash);
    }
}