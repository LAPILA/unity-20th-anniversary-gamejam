using UnityEngine;

public class CircuitGroupController : MonoBehaviour
{
    [Header("Group Settings")]
    [Tooltip("이 그룹에 포함된 모든 회로 (3개 이상도 가능)")]
    public CircuitActivator[] circuits;

    [Tooltip("이 그룹이 제어할 문")]
    public DoorController targetDoor;

    private bool _isDoorOpen = false;

    /// <summary>
    /// 회로 중 하나의 상태가 변경되었을 때 CircuitActivator에서 호출됩니다.
    /// </summary>
    public void OnCircuitStateChanged()
    {
        if (circuits == null || circuits.Length == 0)
        {
            Debug.LogWarning("회로가 그룹에 설정되지 않았습니다.");
            return;
        }

        // 모든 회로가 활성화되었는지 검사
        bool allActive = true;
        foreach (var circuit in circuits)
        {
            if (!circuit.IsActive)
            {
                allActive = false;
                break;
            }
        }

        // 상태가 바뀐 경우에만 문을 제어
        if (allActive && !_isDoorOpen)
        {
            targetDoor?.OpenDoor();
            _isDoorOpen = true;
            Debug.Log("문이 열립니다!");
        }
        else if (!allActive && _isDoorOpen)
        {
            targetDoor?.CloseDoor();
            _isDoorOpen = false;
            Debug.Log("문이 닫힙니다.");
        }
    }
}
