using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class Explode : MonoBehaviour
{
    public bool IsPrepared => prepared;
    private Rigidbody rb;
    private bool prepared = false;
    private bool released = false;
    private Vector3 explosionPoint;

    [Header("Explosion Settings")]
    public float cleanupDelay = 5f;
    public float explosionRadius = 5f;
    public float upwardsModifier = 2f;
    public float explosionForce = 500f;

    [Header("Time Stop")]
    [Tooltip("폭발 후 파편이 멈추는 시간")]
    public float stopDelay = 0.5f;

    [Header("Effect")]
    public Animator explosionAnimator;
    public string animatorTrigger = "Explode";
    public ParticleSystem explosionParticles;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 안전하게 Rigidbody가 없을 경우 Prepare로 간주
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            if (rb == null || rb.isKinematic)
            {
                PrepareExplosion();
            }
            else
            {
                // 이미 물리 활성 상태면 즉시 터지게 함
                ExplodeNow(transform.position);
            }
        }
    }

    // G키로 "준비" (애니메이션 재생, 필요한 Collider/Rigidbody 추가, kinematic으로 고정)
    public void PrepareExplosion()
    {
        if (prepared) return;
        prepared = true;
        explosionPoint = transform.position;
        Debug.Log($"{name}: PrepareExplosion");

        if (explosionAnimator != null)
            explosionAnimator.SetTrigger(animatorTrigger);
        if (explosionParticles != null)
            explosionParticles.Play();

        // 부모 Rigidbody 준비(없으면 추가) — 준비 상태로 고정
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 부모에 Collider 없으면 간단한 Collider 추가
        if (GetComponent<Collider>() == null)
            AddSimpleCollider(gameObject);

        // 자식들에 대해 Collider/Rigidbody 생성 및 kinematic으로 고정
        Rigidbody[] childRbs = GetComponentsInChildren<Rigidbody>(true);
        foreach (var childTransform in GetComponentsInChildren<Transform>(true))
        {
            if (childTransform == transform) continue;
            GameObject child = childTransform.gameObject;

            if (child.GetComponent<Collider>() == null)
                AddSimpleCollider(child);

            Rigidbody childRb = child.GetComponent<Rigidbody>();
            if (childRb == null)
                childRb = child.AddComponent<Rigidbody>();

            childRb.isKinematic = true;
            childRb.useGravity = false;
        }
    }

    // TimeGun 또는 외부에서 호출: 준비 여부 상관없이 폭발 트리거
    // TimeGun이 이전에 사용하던 이름을 호출해도 동작하도록 ReleaseDebris로도 노출
    public void TriggerExplosionByTimeGun(Vector3 hitPoint)
    {
        // 만약 이미 폭발된 상태면 무시
        if (released) return;

        // 준비된 상태이면 준비 지점(또는 히트 포인트)을 사용해 폭발
        ExplodeNow(hitPoint);
    }

    public void ReleaseDebris(Vector3 hitPoint) => TriggerExplosionByTimeGun(hitPoint);

    // 실제 폭발 처리: 부모 + 모든 자식 Rigidbody 활성화하고 폭발력 적용
    public void ExplodeNow(Vector3 forceOrigin)
    {
        if (released) return;

        // 자식들이 아직 준비되지 않았다면, 강제로 준비시킨다.
        if (!prepared)
            PrepareExplosion();

        released = true;
        Debug.Log($"{name}: ExplodeNow at {forceOrigin}");

        // 코루틴으로 폭발 및 시간 정지 로직 실행
        StartCoroutine(ExplodeRoutine(forceOrigin));
    }

    private IEnumerator ExplodeRoutine(Vector3 forceOrigin)
    {
        // 1. 부모 포함 모든 Rigidbody 수집
        var rbs = new List<Rigidbody>();
        Rigidbody parentRb = GetComponent<Rigidbody>();
        if (parentRb != null && !rbs.Contains(parentRb))
            rbs.Add(parentRb);

        foreach (var rbChild in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rbChild != null && !rbs.Contains(rbChild))
                rbs.Add(rbChild);
        }

        // 2. 초기 폭발력 적용 및 파편 물리 활성화
        foreach (var r in rbs)
        {
            if (r == null) continue;

            // 자식 오브젝트들을 물리적으로 분리 (부모에서 떼기)
            if (r.transform != transform)
                r.transform.SetParent(null);

            r.isKinematic = false;
            r.useGravity = true;

            r.AddExplosionForce(explosionForce, forceOrigin, explosionRadius, upwardsModifier);
            r.AddTorque(Random.onUnitSphere * (explosionForce * 0.01f), ForceMode.Impulse);
        }

        // 3. 시각적 메인 오브젝트 비활성화 (파편만 남기기)
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;

        // 4. [시간 정지] 설정된 시간(0.5초) 동안 파편이 날아가도록 대기
        yield return new WaitForSeconds(stopDelay);

        // 5. [파편 동결] 모든 Rigidbody를 Kinematic으로 고정
        foreach (var r in rbs)
        {
            if (r == null) continue;

            // 물리 정지
            r.isKinematic = true;
            r.useGravity = false;

            // 일정 시간 후 파괴 (메모리 정리)
            Destroy(r.gameObject, cleanupDelay);
        }
    }

    // 유틸: 단순 Collider 추가 (Mesh가 있으면 MeshCollider convex, 없으면 BoxCollider)
    private void AddSimpleCollider(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.convex = true;
        }
        else
        {
            go.AddComponent<BoxCollider>();
        }
    }
}
