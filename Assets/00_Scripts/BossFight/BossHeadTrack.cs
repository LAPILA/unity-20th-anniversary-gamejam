using UnityEngine;

public class BossHeadTrack : MonoBehaviour
{
    [Tooltip("추적할 대상 (주로 Player Transform)")]
    public Transform target;

    [Tooltip("머리가 회전하는 속도")]
    public float rotationSpeed = 5f;

    void Update()
    {
        if (target == null)
        {
            // 플레이어 오브젝트를 태그(Tag)로 찾아 설정 (Awake 등에서 실행하는 것이 더 효율적)
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            return;
        }

        // 1. 플레이어를 향하는 방향 벡터 계산
        Vector3 direction = (target.position - transform.position).normalized;

        // 2. 현재 로컬 축을 기준으로 목표 회전값(Quaternion) 계산
        // 이 보스의 '정면'이 어느 축인지에 따라 조정이 필요할 수 있습니다. (Vector3.forward)
        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // 3. 부드럽게 회전
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }
}