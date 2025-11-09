using UnityEngine;
using System.Collections.Generic; // List를 사용하기 위해 추가

public class DroneEvent : MonoBehaviour
{
    [Header("Trigger Settings")]
    public string targetTag = "Player";

    // [수정] 여러 드론 컴포넌트 목록을 참조합니다.
    [Header("Drone Action - Multi Target")]
    [Tooltip("상태를 변경할 Drone_Explosive 스크립트 목록")]
    public List<Drone_Explosive> targetDrones;

    // 플레이어가 영역에 들어왔을 때 호출됨
    private void OnTriggerEnter(Collider other)
    {
        // 1. 타겟 태그와 일치하는지 확인
        if (other.CompareTag(targetTag))
        {
            Debug.Log($"플레이어({other.name})가 구역에 진입했습니다. 여러 드론 활성화 시작.");

            // 2. 드론 상태 변경 로직 실행
            ActivateAllDronesPatrolling();
        }
    }

    /// <summary>
    /// 대상 드론 목록 전체를 순회하며 Patrolling 상태로 전환을 요청합니다.
    /// </summary>
    private void ActivateAllDronesPatrolling()
    {
        if (targetDrones == null || targetDrones.Count == 0)
        {
            Debug.LogWarning("DroneEvent: 상태를 변경할 대상 드론이 설정되지 않았습니다.");
            return;
        }

        // 목록에 있는 모든 드론을 순회하며 ToggleTimeState() 호출
        foreach (var drone in targetDrones)
        {
            if (drone != null)
            {
                // ToggleTimeState()를 호출하면, Frozen 상태의 드론은 Patrolling으로 전환됩니다.
                drone.ToggleTimeState();
                Debug.Log($"-> 드론({drone.name}) 순찰 상태로 전환 완료.");
            }
        }
    }
}