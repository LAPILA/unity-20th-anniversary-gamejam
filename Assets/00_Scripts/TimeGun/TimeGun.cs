using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Sirenix.OdinInspector;

/// <summary>
/// 시간을 제어하는 총 (Time Gun)
/// - PlayerInput의 "Fire" 액션에 반응합니다.
/// - 크로스헤어(화면 중앙)를 기준으로 Raycast를 발사합니다.
/// - 기본적으로 정지된 오브젝트의 물리, 애니메이션, AI를 활성화시킵니다.
/// </summary>
public class TimeGun : MonoBehaviour
{
    [BoxGroup("References"), Required]
    [Tooltip("플레이어의 PlayerInput 컴포넌트")]
    public PlayerInput playerInput;

    [BoxGroup("References"), Required]
    [Tooltip("플레이어의 메인 카메라")]
    public Camera playerCamera;

    [BoxGroup("Settings")]
    [Tooltip("Raycast의 최대 탐지 거리")]
    public float maxDistance = 100f;

    [BoxGroup("Settings")]
    [Tooltip("디버그 Ray 시각화 시간")]
    public float rayDrawDuration = 1f;

    void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            playerInput.actions["Fire"].performed += OnFirePerformed;
        }
        else
        {
            Debug.LogError("PlayerInput 또는 Actions가 TimeGun에 할당되지 않았습니다!");
        }
    }

    void OnDisable()
    {
        // 2. 이벤트 구독 해제 (메모리 누수 방지)
        if (playerInput != null && playerInput.actions != null)
        {
            playerInput.actions["Fire"].performed -= OnFirePerformed;
        }
    }

    /// <summary>
    /// "Fire" 입력이 감지되었을 때 호출되는 콜백
    /// </summary>
    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        // 3. 발사 함수 호출
        Fire();
    }

    /// <summary>
    /// 크로스헤어를 기준으로 Raycast를 발사합니다.
    /// </summary>
    [Button("Test Fire")]
    public void Fire()
    {
        if (playerCamera == null)
        {
            Debug.LogError("Player Camera가 TimeGun에 할당되지 않았습니다!");
            return;
        }

        RaycastHit hit;
        // 4. 화면 정중앙(크로스헤어)에서 Ray를 생성합니다. (가장 정확한 방식)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // 5. 디버그용 Ray 그리기
        Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.cyan, rayDrawDuration);

        // 6. Raycast 발사
        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            // 7. [요청 사항] 맞은 객체의 이름 Debug.Log로 출력
            Debug.Log("TimeGun Hit: " + hit.collider.gameObject.name, hit.collider.gameObject);

            // 8. 맞은 대상의 "시간을 흐르게" 하는 로직 실행
            ActivateTimeForObject(hit.collider.gameObject);
        }
    }

    /// <summary>
    /// 대상 오브젝트의 시간을 활성화시킵니다. (물리, 애니메이션, AI)
    /// </summary>
    private void ActivateTimeForObject(GameObject target)
    {
        // A. 물리 활성화 (isKinematic 해제)
        ActivatePhysics(target);

        // B. 애니메이션 활성화 (Animator 속도 1로)
        ActivateAnimation(target);

        // C. AI 활성화 (NavMeshAgent 재시작)
        ActivateAI(target);
    }

    /// <summary>
    /// 대상의 Rigidbody를 찾아 Kinematic을 해제하고 중력을 활성화합니다.
    /// (컴포넌트가 없으면 조용히 무시 - 메모리 효율적)
    /// </summary>
    private void ActivatePhysics(GameObject target)
    {
        Rigidbody hitRb = target.GetComponent<Rigidbody>();
        if (hitRb != null && hitRb.isKinematic)
        {
            hitRb.isKinematic = false;
            hitRb.useGravity = true; // 기본적으로 중력은 켜는 것으로 가정
            Debug.Log(target.name + "의 물리 활성화.", target);
        }
    }

    /// <summary>
    /// 대상의 Animator를 찾아 Speed를 1로 설정합니다.
    /// (컴포넌트가 없거나 이미 활성화 상태면 조용히 무시)
    /// </summary>
    private void ActivateAnimation(GameObject target)
    {
        Animator animator = target.GetComponent<Animator>();
        if (animator != null && animator.speed == 0)
        {
            animator.speed = 1f;
            Debug.Log(target.name + "의 애니메이션 활성화.", target);
        }
    }

    /// <summary>
    /// 대상의 NavMeshAgent(AI)를 찾아 isStopped를 false로 설정합니다.
    /// (컴포넌트가 없거나 이미 활성화 상태면 조용히 무시)
    /// </summary>
    private void ActivateAI(GameObject target)
    {
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isStopped)
        {
            agent.isStopped = false;
            Debug.Log(target.name + "의 AI 활성화.", target);
        }
    }
}