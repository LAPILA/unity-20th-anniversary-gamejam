using UnityEngine;
using System.Collections;

/// <summary>
/// 문을 열고 닫는 애니메이션을 처리합니다.
/// Time.timeScale에 영향을 받지 않도록 Time.unscaledDeltaTime을 사용합니다.
/// </summary>
public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("문이 열릴 때 이동할 최종 위치 (월드 좌표)")]
    public Vector3 openPosition = new Vector3(0, 5, 0); // Example: moves 5 units up

    [Tooltip("문이 닫힐 때의 원래 위치")]
    private Vector3 _closedPosition;

    [Tooltip("문 개폐에 걸리는 시간 (초)")]
    public float moveDuration = 1.0f;

    private bool _isOpening = false;
    private bool _isOpen = false;

    void Awake()
    {
        _closedPosition = transform.position;
    }

    /// <summary>
    /// 문을 엽니다. 이미 열려 있거나 열리는 중이면 무시됩니다.
    /// </summary>
    public void OpenDoor()
    {
        if (_isOpen || _isOpening) return;

        Debug.Log("Door: Opening sequence started.");
        StartCoroutine(MoveDoorRoutine(openPosition, true));
    }

    /// <summary>
    /// 문을 닫습니다. 이미 닫혀 있거나 닫히는 중이면 무시됩니다.
    /// </summary>
    public void CloseDoor()
    {
        if (!_isOpen || _isOpening) return;

        Debug.Log("Door: Closing sequence started.");
        StartCoroutine(MoveDoorRoutine(_closedPosition, false));
    }

    /// <summary>
    /// 문을 목표 위치로 부드럽게 이동시키는 코루틴
    /// </summary>
    private IEnumerator MoveDoorRoutine(Vector3 targetPosition, bool stateAfterMove)
    {
        _isOpening = true;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < moveDuration)
        {
            // 💡 Time.unscaledDeltaTime을 사용하여 Time.timeScale = 0 상태에서도 움직이게 합니다.
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        // Ensure door reaches final position
        transform.position = targetPosition;
        _isOpening = false;
        _isOpen = stateAfterMove;

        Debug.Log($"Door state updated: {(stateAfterMove ? "OPEN" : "CLOSED")}");
    }
}