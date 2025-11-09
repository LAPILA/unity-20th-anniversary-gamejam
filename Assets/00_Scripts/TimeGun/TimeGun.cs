using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Sirenix.OdinInspector;

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
        if (playerInput != null && playerInput.actions != null)
        {
            playerInput.actions["Fire"].performed -= OnFirePerformed;
        }
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        Fire();
    }

    [Button("Test Fire")]
    public void Fire()
    {
        if (playerCamera == null)
        {
            Debug.LogError("Player Camera가 TimeGun에 할당되지 않았습니다!");
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.cyan, rayDrawDuration);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            GameObject target = hit.collider.gameObject;
            Debug.Log("TimeGun Hit: " + target.name, target);

            // --- [A / B 로직] ---
            // A: 이 오브젝트가 '특별한' 로직을 가지고 있는지 확인 (ITimeActivatable)
            // (GetComponentInParent: 하위 콜라이더를 쏴도 부모의 메인 로직을 찾을 수 있게 함)
            ITimeActivatable timeObject = target.GetComponentInParent<ITimeActivatable>();

            if (timeObject != null)
            {
                // A-1: 특별한 로직이 있다면, 그 로직을 실행 (예: Drone_Explosive.cs)
                timeObject.ToggleTimeState();
            }
            else
            {
                // B-1: 특별한 로직이 없다면, '기본' 토글 로직 실행 (멍청한 상자, 문 등)
                ActivateDefaultTimeToggle(target, hit.point);
            }
            // --- [A / B 로직 종료] ---
        }
    }
    /// <summary>
    /// ITimeActivatable을 구현하지 않은 '일반' 오브젝트의
    /// 물리/애니메이션/AI 상태를 강제로 토글(On/Off)합니다.
    /// </summary>
    private void ActivateDefaultTimeToggle(GameObject target, Vector3 hitPoint)
    {
        // 1. 물리 토글 (Rigidbody) + Explode 트리거
        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // [수정된 부분] Explode 컴포넌트를 부모에서 찾습니다 (자식 콜라이더 피격 대비)
            Explode explode = target.GetComponentInParent<Explode>();

            if (rb.isKinematic)
            {
                // [상태: 정지 -> 활성]
                rb.isKinematic = false;
                rb.useGravity = true;
                Debug.Log(target.name + ": Rigidbody 활성화됨", target);

                // [Explode 처리] Explode 컴포넌트가 있고 '준비(prepared)'된 상태라면 폭발
                if (explode != null && explode.IsPrepared)
                {
                    Debug.Log(target.name + ": Explode.cs 감지 및 원격 폭발 실행", target);
                    explode.TriggerExplosionByTimeGun(hitPoint);
                }
            }
            else
            {
                // [상태: 활성 -> 정지]
                rb.isKinematic = true;
                rb.useGravity = false;
                Debug.Log(target.name + ": Rigidbody 비활성화됨", target);
            }
        }

        // 2. 애니메이션 토글 (Animator)
        Animator animator = target.GetComponent<Animator>();
        if (animator != null)
        {
            float newSpeed = (animator.speed == 0f) ? 1f : 0f;
            animator.speed = newSpeed;
            Debug.Log(target.name + ": Animator 토글 -> Speed: " + animator.speed, target);
        }

        // 3. AI 토글 (NavMeshAgent)
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = !agent.isStopped;
            Debug.Log(target.name + ": NavMeshAgent 토글 -> isStopped: " + agent.isStopped, target);
        }
    }
}
