using UnityEngine;

public class CircuitGroupController : MonoBehaviour
{
    public TimeCircuit[] circuits;
    public DoorController door;

    void Start()
    {
        foreach (var circuit in circuits)
        {
            circuit.OnCircuitStateChanged += OnCircuitStateChanged;
        }
    }

    void OnCircuitStateChanged(TimeCircuit circuit, bool isFlowing)
    {
        // 모든 회로가 켜져 있는지 확인
        bool allOn = true;
        foreach (var c in circuits)
        {
            if (!c.IsFlowing)
            {
                allOn = false;
                break;
            }
        }

        if (allOn)
            door.OpenDoor();
        else
            door.CloseDoor();
    }
}
