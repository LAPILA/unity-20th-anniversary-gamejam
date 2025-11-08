using UnityEngine;
using Unity.Cinemachine; // VCam 사용을 위해 추가
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// NPC에게 부착되어 대화 상호작용을 처리합니다.
/// IInteractable 인터페이스를 구현합니다.
/// </summary>
public class NPC_Dialogue : MonoBehaviour, IInteractable
{
    [BoxGroup("Virtual Camera"), Required]
    [Tooltip("이 NPC와 대화할 때 활성화할 Cinemachine Virtual Camera")]
    [SerializeField] private CinemachineCamera virtualCamera;

    [BoxGroup("Dialogue"), Required]
    [Tooltip("이 NPC가 재생할 대화 데이터 목록")]
    [SerializeField] private List<DialogueData> dialogueList;

    [BoxGroup("Dialogue")]
    [Tooltip("true: 마지막 대화를 계속 반복 / false: 목록 처음으로 돌아감")]
    [SerializeField] private bool loopLastDialogue = true;

    [BoxGroup("Visuals")]
    [Tooltip("플레이어가 범위에 들어왔을 때 활성화할 아웃라인 (선택 사항)")]
    [SerializeField] private GameObject outlineObject;

    private int _dialogueIndex = 0;
    private bool _isInteracting = false;
    private PlayerInteractor _currentPlayerInteractor;

    void Awake()
    {
        if (virtualCamera != null) virtualCamera.Priority = 0;
        if (outlineObject != null) outlineObject.SetActive(false);
    }

    /// <summary>
    /// [IInteractable] 플레이어가 상호작용 키를 눌렀을 때
    /// </summary>
    public void Interact(PlayerInteractor interactor)
    {
        if (_isInteracting || dialogueList.Count == 0) return;

        _isInteracting = true;
        _currentPlayerInteractor = interactor;

        // 1. 플레이어 이동 및 상호작용 잠금
        PlayerController pc = interactor.GetPlayerController();
        if (pc != null) pc.LockMovement(true);
        interactor.LockInteraction(true); // "E" 키 연타 방지

        // 2. VCam 활성화 (Priority를 높여서)
        if (virtualCamera != null) virtualCamera.Priority = 100;
        
        // 3. 현재 순서에 맞는 대화 데이터 가져오기
        DialogueData dataToPlay = dialogueList[_dialogueIndex];

        // 4. 대화 매니저에게 대화 시작 요청 (종료 시 호출할 함수 전달)
        DialogueManager.Instance.StartDialogue(dataToPlay, OnDialogueFinished);

        // 5. 대화 인덱스 관리 (관리 용이성)
        if (_dialogueIndex < dialogueList.Count - 1)
        {
            _dialogueIndex++; // 다음 대화로 넘어감
        }
        else if (!loopLastDialogue)
        {
            _dialogueIndex = 0; // 처음으로 리셋
        }
        // (loopLastDialogue가 true면 인덱스가 마지막에 고정됨)
    }

    /// <summary>
    /// [IInteractable] 플레이어가 범위에 들어옴
    /// </summary>
    public void OnPlayerEnterRange()
    {
        if (outlineObject != null) outlineObject.SetActive(true);
    }

    /// <summary>
    /// [IInteractable] 플레이어가 범위에서 나감
    /// </summary>
    public void OnPlayerExitRange()
    {
        if (outlineObject != null) outlineObject.SetActive(false);
    }

    /// <summary>
    /// DialogueManager가 대화를 끝냈을 때 호출하는 콜백 함수
    /// </summary>
    private void OnDialogueFinished()
    {
        // 1. VCam 비활성화 (Priority 원래대로)
        if (virtualCamera != null) virtualCamera.Priority = 0;

        // 2. 플레이어 이동 및 상호작용 잠금 해제
        if (_currentPlayerInteractor != null)
        {
            PlayerController pc = _currentPlayerInteractor.GetPlayerController();
            if (pc != null) pc.LockMovement(false);
            _currentPlayerInteractor.LockInteraction(false);
        }

        _isInteracting = false;
        _currentPlayerInteractor = null;
    }
}