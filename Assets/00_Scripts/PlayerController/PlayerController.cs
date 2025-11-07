using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;
using MoreMountains.Feedbacks;
using DG.Tweening;

/*
 * [ StasisPlayerController ]
 * 1. Unity 6 Input System (Send Messages 방식)을 사용합니다.
 * 2. CharacterController 기반의 1인칭 물리 이동을 구현합니다.
 * 3. 풀 바디 모델(PlayerModel)과 1인칭 카메라(FirstPersonCamera)의 회전을 분리하여 처리합니다.
 * 4. Odin Inspector: 인스펙터를 깔끔하게 정리합니다.
 * 5. Feel: 점프, 착지 피드백을 위해 MMF_Player를 연동합니다.
 * 6. DOTween: 이동 시 카메라 헤드밥(Head Bob)을 구현합니다.
 */
[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class StasisPlayerController : MonoBehaviour
{
    #region Odin: 컴포넌트 레퍼런스
    [TitleGroup("Component References")]
    [Required(ErrorMessage = "캐릭터 컨트롤러가 필요합니다")]
    [SerializeField] private CharacterController controller;

    [Required(ErrorMessage = "1인칭 카메라가 필요합니다")]
    [SerializeField] private Camera firstPersonCamera;

    [Required(ErrorMessage = "풀 바디 모델(아바타) 오브젝트가 필요합니다")]
    [SerializeField] private GameObject playerModel;
    #endregion

    #region Odin: 이동 및 중력 설정
    [TitleGroup("Movement Settings")]
    [SuffixLabel("m/s")]
    [Range(1f, 10f)]
    [SerializeField] private float moveSpeed = 5.0f;

    [SuffixLabel("m/s")]
    [Range(1f, 5f)]
    [SerializeField] private float jumpHeight = 2.0f;

    [SuffixLabel("m/s²")]
    [SerializeField] private float gravityValue = -19.62f; // 일반 중력(9.81)의 2배로 묵직하게
    #endregion

    #region Odin: 시야 및 민감도
    [TitleGroup("Look Settings")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float lookSensitivity = 0.5f;

    [Range(-90f, 0f)]
    [SerializeField] private float minCameraPitch = -80f;

    [Range(0f, 90f)]
    [SerializeField] private float maxCameraPitch = 80f;
    #endregion

    #region Odin: 헤드밥 (DOTween)
    [TitleGroup("Head Bob (DOTween)")]
    [SerializeField] private bool enableHeadBob = true;

    [ShowIf("enableHeadBob")]
    [Range(0.1f, 2f)]
    [SerializeField] private float bobFrequency = 1.0f; // 1초에 몇 번 왕복

    [ShowIf("enableHeadBob")]
    [Range(0.01f, 0.2f)]
    [SerializeField] private float bobAmplitude = 0.05f; // 이동 폭

    private Vector3 cameraOriginalLocalPos;
    private Sequence bobTween;
    #endregion

    #region Odin: 게임 필 (Feel)
    [TitleGroup("Game Feel")]
    [Required(ErrorMessage = "점프 피드백을 할당해야 합니다")]
    [SerializeField] private MMF_Player jumpFeedback;

    [Required(ErrorMessage = "착지 피드백을 할당해야 합니다")]
    [SerializeField] private MMF_Player landFeedback;

    // [BoxGroup("5. Game Feel (MMF)/Footsteps")]
    // [SerializeField] private MMF_Player footstepFeedback;
    // [BoxGroup("5. Game Feel (MMF)/Footsteps")]
    // [SerializeField] private float footstepInterval = 0.5f;
    // private float footstepTimer = 0f;
    #endregion

    #region Private 상태 변수
    private PlayerInput playerInput;

    // 입력값 저장
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpInput;

    // 물리 상태
    private Vector3 playerVelocity;
    private bool isGrounded;
    private float cameraPitch = 0.0f;
    #endregion

    #region 초기화
    private void Awake()
    {
        // 컴포넌트 자동 할당 (Odin의 [Required]가 있지만, 만약을 대비)
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (firstPersonCamera == null)
            firstPersonCamera = Camera.main;

        // 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        cameraOriginalLocalPos = firstPersonCamera.transform.localPosition;

        // DOTween 헤드밥 시퀀스 생성 (재사용)
        if (enableHeadBob)
        {
            bobTween = DOTween.Sequence();
            bobTween.Append(firstPersonCamera.transform.DOLocalMoveY(cameraOriginalLocalPos.y + bobAmplitude, 0.5f / bobFrequency))
                   .Append(firstPersonCamera.transform.DOLocalMoveY(cameraOriginalLocalPos.y, 0.5f / bobFrequency))
                   .SetLoops(-1)
                   .Pause(); // 시작 시 정지
        }
    }
    #endregion

    #region Input System (Send Messages)
    // "Move" Action (Vector2)
    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    // "Look" Action (Vector2)
    private void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    // "Jump" Action (Button)
    private void OnJump(InputValue value)
    {
        jumpInput = value.isPressed;
    }
    #endregion

    #region Update 루프
    private void Update()
    {
        // 매 프레임마다 상태 갱신
        HandleGroundedCheck();
        HandleGravity();

        // 입력 기반 처리
        HandleMovement();
        HandleLook();
        HandleJump();

        // 효과
        HandleHeadBob();
    }

    private void HandleGroundedCheck()
    {
        bool wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;

        // 방금 땅에 착지한 경우
        if (!wasGrounded && isGrounded && playerVelocity.y < -2f) // 떨어지다가 착지
        {
            // Debug.Log("Landed!");
            landFeedback?.PlayFeedbacks();

            // 착지 시 속도 초기화 (바닥에 튕기지 않도록)
            playerVelocity.y = -2f;
        }
    }

    private void HandleGravity()
    {
        // 땅에 닿아있으면 중력 천천히 적용 (바닥에 붙이기)
        if (isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = -2f; // 땅에 붙이는 힘
        }
        else
        {
            // 공중에 있으면 중력 가속
            playerVelocity.y += gravityValue * Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        // 1. 입력값을 (x, z) 벡터로 변환
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        // 2. 이동 벡터를 플레이어가 바라보는 방향(transform.forward/right) 기준으로 변환
        moveDirection = transform.right * moveDirection.x + transform.forward * moveDirection.z;
        moveDirection.Normalize(); // 대각선 이동 속도 보정

        // 3. 최종 이동 (물리 + 입력)
        controller.Move((moveDirection * moveSpeed + playerVelocity) * Time.deltaTime);

        // 4. 애니메이터 파라미터 설정 (모델이 있다면)
        // animator.SetFloat("MoveSpeed", moveDirection.magnitude);
    }

    private void HandleLook()
    {
        if (lookInput.sqrMagnitude < 0.01f) return;

        float lookX = lookInput.x * lookSensitivity;
        float lookY = lookInput.y * lookSensitivity;

        // 1. 카메라 상하 회전 (Pitch)
        // - Y 입력(마우스 상하)을 X축 회전(카메라 상하)에 사용
        cameraPitch -= lookY;
        cameraPitch = Mathf.Clamp(cameraPitch, minCameraPitch, maxCameraPitch);

        // 카메라는 상하 회전만 담당
        firstPersonCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

        // 2. 플레이어 좌우 회전 (Yaw)
        // - X 입력(마우스 좌우)을 Y축 회전(플레이어 좌우)에 사용
        // - 플레이어 전체(모델 포함)를 Y축 기준으로 회전
        transform.Rotate(Vector3.up * lookX);
    }

    private void HandleJump()
    {
        if (jumpInput && isGrounded)
        {
            // Debug.Log("Jump!");
            // v = sqrt(h * -2 * g) -> 점프 공식
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);

            // Feel 피드백 재생
            jumpFeedback?.PlayFeedbacks();
        }
        // 점프 입력 초기화 (버튼을 떼도 계속 점프하는 것 방지)
        jumpInput = false;
    }

    private void HandleHeadBob()
    {
        if (!enableHeadBob) return;

        // 땅에 붙어있고, 움직이는 중일 때
        if (isGrounded && moveInput.magnitude > 0.1f)
        {
            if (!bobTween.IsActive() || !bobTween.IsPlaying())
            {
                // Debug.Log("Play Bob");
                // 멈춰있던 트윈을 재생 (카메라가 원래 위치가 아닐 수 있으므로 From 설정)
                firstPersonCamera.transform.DOLocalMoveY(cameraOriginalLocalPos.y, 0.1f).OnComplete(() =>
                {
                    bobTween.Play();
                });
            }
        }
        else // 멈췄거나 공중일 때
        {
            if (bobTween.IsPlaying())
            {
                // Debug.Log("Stop Bob");
                bobTween.Pause();
                // 카메라를 원래 위치로 부드럽게 복귀
                firstPersonCamera.transform.DOLocalMoveY(cameraOriginalLocalPos.y, 0.2f);
            }
        }
    }
    #endregion
}