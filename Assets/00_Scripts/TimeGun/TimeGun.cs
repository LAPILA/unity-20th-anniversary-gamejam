using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Sirenix.OdinInspector;
using System.Collections;

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

    [BoxGroup("Effects (New)")]
    [Tooltip("활성화 성공 시 피격 지점에 생성할 VFX 프리팹")]
    public GameObject hitSuccessVFX;

    [BoxGroup("Effects (New)")]
    [Tooltip("활성화 성공 시 피격 지점에서 재생할 사운드 클립")]
    public AudioClip hitSuccessSound;

    [BoxGroup("Effects (New)")]
    [Tooltip("활성화 성공 사운드 볼륨")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 1f;

    // ▼▼▼ [ 1. 이펙트 지속시간 추가 ] ▼▼▼
    [BoxGroup("Effects (New)")]
    [Tooltip("총구 VFX의 지속 시간(초). (VFX 프리팹 자체에 자동 파괴 기능이 없는 경우 사용)")]
    public float muzzleVFXLifetime = 2.0f;

    [BoxGroup("Effects (New)")]
    [Tooltip("피격 성공 VFX의 지속 시간(초). (VFX 프리팹 자체에 자동 파괴 기능이 없는 경우 사용)")]
    public float hitVFXLifetime = 3.0f;
    // ▲▲▲ [ 1. 이펙트 지속시간 추가 ] ▲▲▲


    private bool isFiring = false;

    // (OnEnable, OnDisable, OnFireInput, Fire 함수는 동일)
    #region Input and Fire
    void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            // .performed는 씬 로드 시 원치 않게 실행될 수 있으므로,
            // "눌리는 순간"만 감지하는 .started로 변경합니다.
            playerInput.actions["Fire"].started += OnFireInput;
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
            // .performed에서 .started로 변경
            playerInput.actions["Fire"].started -= OnFireInput;
        }
    }

    private void OnFireInput(InputAction.CallbackContext context)
    {
        Fire();
    }

    [Button("Test Fire")]
    public void Fire()
    {
        if (isFiring) return;
        StartCoroutine(FireSequence());
    }
    #endregion

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
            yield break;
        }

        yield return new WaitForSeconds(fireDelay);

        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound);
        }

        // VFX 생성
        if (muzzleFlashVFX != null)
        {
            // ▼▼▼ [ 2. VFX 부모 설정 및 자동 파괴 로직 추가 ] ▼▼▼

            // 총구(muzzlePoint)가 설정되었으면 그 위치/회전/부모로, 아니면 카메라를 기준으로 설정
            Transform spawnPoint = muzzlePoint != null ? muzzlePoint : playerCamera.transform;

            // Instantiate의 4번째 인자(parent)를 spawnPoint로 설정하여 VFX가 총구를 따라다니게 함
            GameObject vfxInstance = Instantiate(muzzleFlashVFX, spawnPoint.position, spawnPoint.rotation, spawnPoint);

            // [중요] VFX가 스스로 파괴되지 않을 경우를 대비해, 지정된 시간 후에 강제 파괴
            Destroy(vfxInstance, muzzleVFXLifetime);

            // --- [ 기존 코드 ] ---
            // Vector3 vfxPos = muzzlePoint != null ? muzzlePoint.position : playerCamera.transform.position + playerCamera.transform.forward;
            // Quaternion vfxRot = muzzlePoint != null ? muzzlePoint.rotation : playerCamera.transform.rotation;
            // Instantiate(muzzleFlashVFX, vfxPos, vfxRot);

            // ▲▲▲ [ 2. 수정 완료 ] ▲▲▲
        }

        PerformRaycast();
        isFiring = false;
    }

    // (PerformRaycast 함수는 동일)
    #region Raycast Logic
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
                PlayHitEffects(hit.point);
            }
            else
            {
                ActivateDefaultTimeToggle(target, hit.point);
            }
        }
    }

    // (ActivateDefaultTimeToggle 함수는 동일)
    private void ActivateDefaultTimeToggle(GameObject target, Vector3 hitPoint)
    {
        PlayHitEffects(hitPoint);

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
    #endregion

    /// <summary>
    /// 지정된 위치에 성공 VFX와 성공 사운드(3D)를 재생합니다.
    /// </summary>
    private void PlayHitEffects(Vector3 position)
    {
        if (hitSuccessSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSuccessSound, position, hitSoundVolume);
        }

        if (hitSuccessVFX != null)
        {
            // ▼▼▼ [ 3. 피격 VFX 자동 파괴 로직 추가 ] ▼▼▼
            GameObject hitVfxInstance = Instantiate(hitSuccessVFX, position, Quaternion.identity);

            // [중요] 피격 VFX도 지정된 시간 후에 강제 파괴
            Destroy(hitVfxInstance, hitVFXLifetime);
            // ▲▲▲ [ 3. 수정 완료 ] ▲▲▲
        }
    }
}