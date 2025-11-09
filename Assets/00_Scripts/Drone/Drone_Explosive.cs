using UnityEngine;
using UnityEngine.AI; // NavMeshAgent (AI 이동) 사용
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// TimeGun (ITimeActivatable)과 연동되는 폭발 드론 AI입니다.
/// Animator를 사용하지 않고 상태 머신(FSM)으로 작동합니다.
/// [AudioSource] 컴포넌트가 필요합니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(AudioSource))] // 1. AudioSource 강제
public class Drone_Explosive : MonoBehaviour, ITimeActivatable
{
    /// <summary>
    /// 드론 AI의 현재 상태
    /// </summary>
    public enum DroneState
    {
        Frozen,     // (t=0) 시간 정지 상태
        Patrolling, // (t=1) 순찰
        Chasing,    // (t=1) 플레이어 추격
        Countdown,  // (t=1) 폭발 카운트다운
        Primed      // (t=0) 폭발 대기 (Countdown 중 정지됨)
    }

    [BoxGroup("State"), ShowInInspector, ReadOnly]
    private DroneState _currentState = DroneState.Frozen;

    [BoxGroup("References"), Required]
    [Tooltip("AI 이동을 위한 NavMeshAgent 컴포넌트")]
    [SerializeField] private NavMeshAgent agent;

    [BoxGroup("References"), Required]
    [Tooltip("카운트다운 시 점멸할 Light 컴포넌트")]
    [SerializeField] private Light countdownLight;

    [BoxGroup("References"), Required]
    [Tooltip("폭발 시 생성할 VFX 프리팹")]
    [SerializeField] private GameObject explosionVFX;

    [BoxGroup("References"), Required]
    [Tooltip("순찰 지점 (Transform) 목록. 2개 이상 권장.")]
    [SerializeField] private List<Transform> patrolPoints;

    [BoxGroup("Audio"), Required]
    [Tooltip("폭발 시 재생할 1회성 오디오 클립")]
    [SerializeField] private AudioClip explosionSound;

    [BoxGroup("AI Stats")]
    [Tooltip("플레이어를 감지할 반경")]
    [SerializeField] private float sightRange = 15f;
    [Tooltip("플레이어에게 접근해 폭발을 시작할 반경")]
    [SerializeField] private float attackRange = 3f;
    [Tooltip("폭발 카운트다운 시간(초)")]
    [SerializeField] private float countdownTime = 1.0f;

    [BoxGroup("Explosion Feedback")]
    [Tooltip("폭발이 플레이어에게 가하는 넉백 힘")]
    [SerializeField] private float explosionForce = 700f;
    [Tooltip("폭발이 피해를 주는 반경")]
    [SerializeField] private float explosionRadius = 5f;
    [Tooltip("플레이어를 인식하기 위한 레이어 마스크")]
    [SerializeField] private LayerMask playerLayer;

    private Transform _playerTransform; // 추격 대상 플레이어
    private Coroutine _countdownCoroutine; // 1초 폭발 코루틴
    private Coroutine _blinkCoroutine; // 점멸 코루틴
    private bool _isPrimed = false; // 원격 폭파 대기 상태
    private float _baseStoppingDistance; // 순찰용 기본 정지 거리

    private AudioSource _audioSource; // 작동 중 루프 사운드용

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        // 1. AudioSource 컴포넌트 가져오기
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = true; // "작동 중" 사운드는 계속 반복됨

        // 2. (t=0) 시작 시 AI는 항상 정지 상태
        agent.isStopped = true;

        // 3. NavMeshAgent의 '기본 정지 거리'를 Inspector 설정값에서 캐시
        _baseStoppingDistance = agent.stoppingDistance;

        // 4. 카운트다운용 라이트 비활성화
        if (countdownLight != null) countdownLight.enabled = false;
    }

    /// <summary>
    /// [ITimeActivatable] TimeGun에 맞았을 때 호출되는 메인 함수
    /// </summary>
    public void ToggleTimeState()
    {
        switch (_currentState)
        {
            // [State 1] 정지 상태일 때
            case DroneState.Frozen:
                // [State 1] 일반 정지 상태였다면, 순찰 시작
                _currentState = DroneState.Patrolling;
                agent.isStopped = false; // NavMeshAgent 활성화
                agent.stoppingDistance = _baseStoppingDistance; // 순찰용 정지 거리 사용

                _audioSource.Play(); // 1. 작동 시작 (사운드 재생)

                GoToRandomPatrolPoint();
                break;

            // [State 7] 폭발 대기 상태일 때
            case DroneState.Primed:
                // [State 7] 폭발 대기 상태였다면, 즉시 원격 폭발
                Explode(); // (Explode 함수에서 사운드 처리)
                break;


            // [State 5] 카운트다운 중일 때
            case DroneState.Countdown:
                // [State 6] 폭발 대기(Primed) 상태로 정지
                _currentState = DroneState.Primed;
                agent.isStopped = true;
                _isPrimed = true;
                agent.stoppingDistance = _baseStoppingDistance;

                _audioSource.Stop(); // 2. 카운트다운 정지 (사운드 중지)

                if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
                if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
                if (countdownLight != null) countdownLight.enabled = false;
                break;

            // [State 2 & 3] 순찰 또는 추격 중일 때
            case DroneState.Patrolling:
            case DroneState.Chasing:
                // [State 1] 일반 정지 상태로 돌아감
                _currentState = DroneState.Frozen;
                agent.isStopped = true;
                agent.stoppingDistance = _baseStoppingDistance;

                _audioSource.Stop(); // 3. 강제 정지 (사운드 중지)
                break;
        }
    }

    /// <summary>
    /// Update()는 오직 (t=1) 활성화 상태일 때만 로직을 실행합니다.
    /// </summary>
    void Update()
    {
        // (t=0) 정지, 카운트다운, 폭발 대기 상태일 때는 아무것도 하지 않음
        if (_currentState == DroneState.Frozen ||
            _currentState == DroneState.Countdown ||
            _currentState == DroneState.Primed)
        {
            return;
        }

        // (t=1) 활성화 상태일 때:
        // 1. 플레이어 탐색
        HandleSight();
        // 2. 상태에 따른 이동 처리
        HandleMovement();
    }

    /// <summary>
    /// 플레이어 탐색 및 상태(순찰/추격) 전환
    /// </summary>
    private void HandleSight()
    {
        if (PlayerInSight())
        {
            // 추격 상태로 처음 전환될 때만 정지 거리를 0으로 설정
            if (_currentState != DroneState.Chasing)
            {
                _currentState = DroneState.Chasing;
                agent.stoppingDistance = 0f; // [FIX] 추격 시에는 정지하지 않고 돌격
            }
        }
        // 플레이어를 추격하다가 시야에서 놓쳤다면
        else if (_currentState == DroneState.Chasing)
        {
            _currentState = DroneState.Patrolling;
            agent.stoppingDistance = _baseStoppingDistance; // [FIX] 다시 순찰용 정지 거리로 복구
            _playerTransform = null; // 추격 대상 리셋
            GoToRandomPatrolPoint(); // 다시 순찰 시작
        }
    }

    /// <summary>
    /// 현재 상태(순찰/추격)에 따른 이동 처리
    /// </summary>
    private void HandleMovement()
    {
        // [State 3] 추격 상태
        if (_currentState == DroneState.Chasing)
        {
            if (_playerTransform == null) return; // (시야 버그 방지)

            // 플레이어를 향해 이동
            agent.SetDestination(_playerTransform.position);

            // [State 4] 공격 범위에 도달하면 카운트다운 시작
            if (Vector3.Distance(transform.position, _playerTransform.position) <= attackRange)
            {
                StartCountdown();
            }
        }
        // [State 2] 순찰 상태
        else if (_currentState == DroneState.Patrolling)
        {
            // [FIX] 목적지에 도착하면 (경로 계산 중이 아니고, 남은 거리가 정지 거리보다 작으면)
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                // [수정됨] 다음 순찰 지점을 '무작위'로 선택
                GoToRandomPatrolPoint();
            }
        }
    }

    /// <summary>
    /// 플레이어가 시야 범위 내에 있는지 확인
    /// </summary>
    private bool PlayerInSight()
    {
        // sightRange 반경 내에서 playerLayer를 가진 콜라이더를 검출
        Collider[] hits = Physics.OverlapSphere(transform.position, sightRange, playerLayer);

        if (hits.Length > 0)
        {
            // (첫 번째 감지된 플레이어를 타겟으로 설정)
            _playerTransform = hits[0].transform;
            return true;
        }
        return false;
    }

    /// <summary>
    /// [수정됨] 순찰 지점 목록에서 '무작위' 지점을 선택하여 이동
    /// </summary>
    private void GoToRandomPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            Debug.LogWarning(gameObject.name + ": 순찰 지점이 설정되지 않았습니다.");
            return;
        }

        // 0부터 (목록 개수 - 1) 사이의 무작위 인덱스 선택
        int newIndex = Random.Range(0, patrolPoints.Count);

        // NavMeshAgent의 목적지를 새 무작위 지점으로 설정
        agent.SetDestination(patrolPoints[newIndex].position);
    }


    /// <summary>
    /// 1초 폭발 카운트다운 시작 (상태 전환)
    /// </summary>
    private void StartCountdown()
    {
        _currentState = DroneState.Countdown;
        agent.isStopped = true; // 폭발을 위해 정지

        _audioSource.Stop(); // 4. 카운트다운 시작 (작동음 중지)

        // 중복 실행 방지
        if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
        if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);

        _countdownCoroutine = StartCoroutine(CountdownRoutine());
        _blinkCoroutine = StartCoroutine(BlinkLightRoutine());
    }

    /// <summary>
    /// 1초 대기 후 Explode() 호출
    /// </summary>
    private IEnumerator CountdownRoutine()
    {
        yield return new WaitForSeconds(countdownTime); // 1초 대기
        Explode(); // 폭발
    }

    /// <summary>
    /// 카운트다운 중 라이트를 0.1초 간격으로 점멸
    /// </summary>
    private IEnumerator BlinkLightRoutine()
    {
        if (countdownLight == null) yield break;

        while (true)
        {
            countdownLight.enabled = !countdownLight.enabled;
            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// 드론 폭발 처리 (주석 처리된 이전 버전)
    /// </summary>
    //private void Explode()
    //{
    //    // ... (이전 Explode 로직) ...
    //}

    private void Explode()
    {
        if (_audioSource != null) _audioSource.Stop();
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);

        Debug.Log(gameObject.name + " EXPLODED!", this);

        if (explosionVFX != null)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (var hit in hits)
        {
            string layerName = LayerMask.LayerToName(hit.gameObject.layer);

            // Player 반응
            if (layerName == "Player")
            {
                HandlePlayerExplosion(hit);
            }

            // Explode 반응
            else if (layerName == "Explode")
            {
                HandleExplodeObject(hit);
            }

            // ExDoor 반응
            else if (layerName == "ExDoor")
            {
                HandleExDoor(hit);
            }
        }

        StopAllCoroutines();
        Destroy(gameObject);
    }
    private void HandlePlayerExplosion(Collider hit)
    {
        PlayerController pc = hit.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        if (pc.TryGetComponent<Rigidbody>(out Rigidbody playerRb))
        {
            playerRb.AddExplosionForce(explosionForce, transform.position, explosionRadius, 1.0f, ForceMode.Impulse);
            Debug.Log("플레이어에게 폭발 넉백 적용!", pc.gameObject);
        }

        PlayerFeedbacks fx = pc.GetComponent<PlayerFeedbacks>();
        if (fx != null)
        {
            fx.ExplosionHit();
            Debug.Log("플레이어에게 폭발 Feel 적용!", fx.gameObject);
        }

        pc.Die();
    }

    private void HandleExplodeObject(Collider hit)
    {
        var explodeScript = hit.GetComponent<Explode>();
        if (explodeScript != null)
        {
            explodeScript.PrepareExplosion();
            Debug.Log($"{hit.name} → prepareExplode 트리거됨");
        }
        else
        {
            Debug.LogWarning($"{hit.name} (Explode Layer)에 Explode 스크립트가 없음!");
        }
    }

    private void HandleExDoor(Collider hit)
    {
        Rigidbody rb = hit.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = hit.gameObject.AddComponent<Rigidbody>();
        }

        rb.mass = 1f;
        rb.AddExplosionForce(explosionForce * 0.5f, transform.position, explosionRadius, 1.0f, ForceMode.Impulse);

        Debug.Log($"{hit.name} (ExDoor) 폭발 반응됨!");
    }
}