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
            ActivateTimeForObject(target, hit.point);
        }
    }

    private void ActivateTimeForObject(GameObject target, Vector3 hitPoint)
    {
        ActivateAnimation(target);
        ActivateAI(target);
        ActivatePhysics(target, hitPoint);
    }

    private void ActivatePhysics(GameObject target, Vector3 hitPoint)
    {
        Rigidbody hitRb = target.GetComponent<Rigidbody>();
        Explode explode = target.GetComponent<Explode>();

        if (hitRb == null) return;

        // 물리력이 꺼져 있는 상태라면 → 켠다
        if (hitRb.isKinematic)
        {
            hitRb.isKinematic = false;
            hitRb.useGravity = true;
            Debug.Log(target.name + ": 물리 활성화됨", target);

            // Explode가 존재하고, '준비(prepared)'된 상태일 때만 폭발
            if (explode != null && explode.IsPrepared)
            {
                explode.TriggerExplosionByTimeGun(hitPoint);
            }
        }
        else
        {
            // 이미 활성화된 경우 → 비활성화
            hitRb.isKinematic = true;
            hitRb.useGravity = false;
            Debug.Log(target.name + ": 물리 비활성화됨", target);
        }
    }

    private void ActivateAnimation(GameObject target)
    {
        Animator animator = target.GetComponent<Animator>();
        if (animator != null && animator.speed == 0)
        {
            animator.speed = 1f;
            Debug.Log(target.name + ": 애니메이션 활성화됨", target);
        }
    }

    private void ActivateAI(GameObject target)
    {
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isStopped)
        {
            agent.isStopped = false;
            Debug.Log(target.name + ": AI 활성화됨", target);
        }
    }
}
