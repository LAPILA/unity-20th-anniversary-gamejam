using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Sirenix.OdinInspector;
using System.Collections; // 1. 코루틴(딜레이)을 위해 추가

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

    [BoxGroup("Effects (New)")]
    [Tooltip("발사 시 재생할 VFX 프리팹")]
    public GameObject muzzleFlashVFX;

    [BoxGroup("Effects (New)")]
    [Tooltip("VFX가 생성될 위치 (총구)")]
    public Transform muzzlePoint;

    [BoxGroup("Effects (New)")]
    [Tooltip("사운드를 재생할 AudioSource 컴포넌트")]
    public AudioSource audioSource;

    [BoxGroup("Effects (New)")]
    [Tooltip("발사 시 재생할 사운드 클립")]
    public AudioClip fireSound;

    [BoxGroup("Effects (New)")]
    [Tooltip("발사 명령 후 실제 발사(Raycast)까지의 딜레이 (초)")]
    public float fireDelay = 0.2f;


    private bool isFiring = false; // 연사 방지 플래그

    void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            playerInput.actions["Fire"].performed += OnFireInput;
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
            // 3. "Zoom"(우클릭) 연결 해제
            playerInput.actions["Fire"].performed -= OnFireInput;
        }
    }

    /// <summary>
    /// 4. 발사 입력(우클릭)을 받으면 코루틴을 실행
    /// </summary>
    private void OnFireInput(InputAction.CallbackContext context)
    {
        Fire();
    }

    /// <summary>
    /// 5. 테스트 버튼 및 공용 발사 함수
    /// </summary>
    [Button("Test Fire")]
    public void Fire()
    {
        // 연사 중이 아닐 때만 발사
        if (isFiring) return;

        StartCoroutine(FireSequence());
    }

    /// <summary>
    /// 6. 발사 시퀀스 (딜레이 -> 이펙트 -> 레이캐스트)
    /// </summary>
    private IEnumerator FireSequence()
    {
        isFiring = true;

        if (playerCamera == null)
        {
            Debug.LogError("Player Camera가 TimeGun에 할당되지 않았습니다!");
            isFiring = false;
            yield break; // 코루틴 즉시 중지
        }

        // --- A. 발사 딜레이 (0.2초) ---
        yield return new WaitForSeconds(fireDelay);

        // --- B. 이펙트 재생 ---

        // 사운드 재생
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound);
        }

        // VFX 생성
        if (muzzleFlashVFX != null)
        {
            // 총구(muzzlePoint)가 설정되었으면 그 위치에, 아니면 카메라 위치에 생성
            Vector3 vfxPos = muzzlePoint != null ? muzzlePoint.position : playerCamera.transform.position + playerCamera.transform.forward;
            Quaternion vfxRot = muzzlePoint != null ? muzzlePoint.rotation : playerCamera.transform.rotation;

            Instantiate(muzzleFlashVFX, vfxPos, vfxRot);
        }

        // --- C. 실제 레이캐스트 발사 ---
        PerformRaycast();

        // --- D. 발사 완료 ---
        isFiring = false;
    }

    /// <summary>
    /// 7. 기존 Fire() 로직을 별도 함수로 분리 (레이캐스트만 담당)
    /// </summary>
    private void PerformRaycast()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.cyan, rayDrawDuration);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            GameObject target = hit.collider.gameObject;
            Debug.Log("TimeGun Hit: " + target.name, target);

            ITimeActivatable timeObject = target.GetComponentInParent<ITimeActivatable>();

            if (timeObject != null)
            {
                timeObject.ToggleTimeState();
            }
            else
            {
                ActivateDefaultTimeToggle(target, hit.point);
            }
        }
    }

    /// <summary>
    /// ITimeActivatable을 구현하지 않은 '일반' 오브젝트의
    /// 물리/애니메이션/AI 상태를 강제로 토글(On/Off)합니다.
    /// </summary>
    private void ActivateDefaultTimeToggle(GameObject target, Vector3 hitPoint)
    {
        // (기존 ActivateDefaultTimeToggle 함수 내용과 동일)
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Explode explode = target.GetComponentInParent<Explode>();
            if (rb.isKinematic)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                Debug.Log(target.name + ": Rigidbody 활성화됨", target);

                if (explode != null && explode.IsPrepared)
                {
                    Debug.Log(target.name + ": Explode.cs 감지 및 원격 폭발 실행", target);
                    explode.TriggerExplosionByTimeGun(hitPoint);
                }
            }
            else
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                Debug.Log(target.name + ": Rigidbody 비활성화됨", target);
            }
        }
        Animator animator = target.GetComponent<Animator>();
        if (animator != null)
        {
            float newSpeed = (animator.speed == 0f) ? 1f : 0f;
            animator.speed = newSpeed;
            Debug.Log(target.name + ": Animator 토글 -> Speed: " + animator.speed, target);
        }
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = !agent.isStopped;
            Debug.Log(target.name + ": NavMeshAgent 토글 -> isStopped: " + agent.isStopped, target);
        }
    }
}