using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    [Tooltip("NPC가 바라볼 플레이어 캐릭터의 Transform")]
    // **주목:** 이제 Camera 대신 Player의 Transform을 연결해야 합니다.
    [SerializeField] private Transform playerTransform;

    // (선택 사항: 회전 속도 조절)
    [SerializeField] private float rotationSpeed = 5f;
    public bool CanRotate { get; set; } = true;
    void Start()
    {
        // 런타임에 Player Transform을 찾는 안전장치 추가 (선택 사항)
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (!CanRotate || playerTransform == null) return;

        // 1. 바라볼 목표 위치 계산 (Y축 고정)
        Vector3 targetPosition = playerTransform.position;
        targetPosition.y = transform.position.y;

        // 2. 목표 방향 계산
        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction == Vector3.zero) return;

        // 3. 목표 회전 계산
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // 💥💥💥 4. 180도 회전 보정 적용 (핵심 수정) 💥💥💥
        // NPC 모델의 기본 정면이 뒤를 향하는 문제를 해결합니다.
        targetRotation *= Quaternion.Euler(0, 180f, 0);

        // 5. 부드러운 회전 적용
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}