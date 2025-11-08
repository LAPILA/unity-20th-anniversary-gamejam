using UnityEngine;
using System.Collections.Generic;

public class FrontDoorPuzzle : MonoBehaviour
{
    // === 인스펙터에서 설정할 변수들 ===
    [Header("Door Settings")]
    public GameObject doorObject;           // 문 오브젝트 (Animation 컴포넌트가 부착되어 있어야 함)
    public float requiredTime = 1f;         // 문이 열리기 위해 필요한 시간 (1초)
    public string targetTag = "Barrel";     // 카운트할 오브젝트의 태그 ("Barrel"로 설정)

    [Header("Animation Clip Names")]
    public string openClipName = "Open";    // 문 열림 애니메이션 클립 이름
    public string closeClipName = "Close";  // 문 닫힘 애니메이션 클립 이름


    // === 내부 로직용 변수들 ===
    private List<Collider> objectsInTrigger = new List<Collider>(); // 현재 트리거 안에 있는 타겟 오브젝트 목록
    private float conditionStartTime = 0f; // 조건이 만족되기 시작한 시간 기록
    private bool isDoorOpen = false;       // 문이 현재 열려 있는지 여부

    // **핵심 변경:** Legacy Animation 컴포넌트
    private Animation doorAnimation;

    void Start()
    {
        if (doorObject == null)
        {
            Debug.LogError("Door Object가 할당되지 않았습니다. 인스펙터에서 설정해주세요.");
            return;
        }

        // 1. Animation 컴포넌트 가져오기
        doorAnimation = doorObject.GetComponent<Animation>();

        if (doorAnimation == null)
        {
            Debug.LogError("Door Object에 Legacy Animation 컴포넌트(🎶 아이콘)가 없습니다! 컴포넌트를 추가해주세요.");
            return;
        }

        // 2. 초기화: 문을 닫힌 상태로 초기화합니다.
        // 클립이 등록되어 있다면, 닫힘 애니메이션을 재생하여 닫힌 위치로 이동시킵니다.
        if (doorAnimation.GetClip(closeClipName) != null)
        {
            doorAnimation.Play(closeClipName);
        }
        else
        {
            Debug.LogWarning($"Close 클립 '{closeClipName}'이 Animation 컴포넌트에 등록되지 않았습니다.");
        }
    }

    void Update()
    {
        // 현재 트리거 안에 타겟 오브젝트가 2개 이상 있는지 여부
        bool conditionMet = objectsInTrigger.Count >= 2;

        if (!isDoorOpen)
        {
            // ===================================
            // 1. 닫힌 상태 (Close): 문 열림 조건만 확인
            // ===================================
            if (conditionMet)
            {
                // 조건이 처음 만족된 시점이라면 시간 기록 시작
                if (conditionStartTime == 0f)
                {
                    conditionStartTime = Time.time;
                }

                // 조건이 requiredTime(예: 1초) 이상 지속되었는지 확인
                if (Time.time >= conditionStartTime + requiredTime)
                {
                    OpenDoor();
                }
            }
            else
            {
                // 조건이 만족되지 않으면 타이머 리셋
                conditionStartTime = 0f;
            }
        }
        else // isDoorOpen == true
        {
            // ===================================
            // 2. 열린 상태 (Open): 문 닫힘 조건만 확인
            // ===================================
            if (!conditionMet) // 조건(2개 이상)이 해제됨 (1개 이하)
            {
                CloseDoor();
            }
        }
    }

    // 오브젝트가 트리거 영역에 들어왔을 때 호출
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            if (!objectsInTrigger.Contains(other))
            {
                objectsInTrigger.Add(other);
            }
        }
    }

    // 오브젝트가 트리거 영역에서 나갔을 때 호출
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            if (objectsInTrigger.Contains(other))
            {
                objectsInTrigger.Remove(other);
            }
        }
    }

    // 문을 열고 상태를 변경하는 함수
    private void OpenDoor()
    {
        isDoorOpen = true;
        conditionStartTime = 0f; // 타이머 초기화 (더 이상 필요 없음)
        Debug.Log("조건 만족! 문 열림 애니메이션 실행.");

        if (doorAnimation != null && doorAnimation.GetClip(openClipName) != null)
        {
            doorAnimation.Play(openClipName);
        }
        else
        {
            Debug.LogWarning($"Open 클립 '{openClipName}'을 재생할 수 없습니다.");
        }
    }

    // 문을 닫고 상태를 변경하는 함수
    private void CloseDoor()
    {
        // 문을 닫는 순간 조건이 다시 만족될 수 있으므로, Update에서 닫힘 조건이 
        // 만족되었을 때만 이 함수가 호출되게 됩니다.
        isDoorOpen = false;
        Debug.Log("조건 해제! 문 닫힘 애니메이션 실행.");

        if (doorAnimation != null && doorAnimation.GetClip(closeClipName) != null)
        {
            doorAnimation.Play(closeClipName);
        }
        else
        {
            Debug.LogWarning($"Close 클립 '{closeClipName}'을 재생할 수 없습니다.");
        }
    }
}