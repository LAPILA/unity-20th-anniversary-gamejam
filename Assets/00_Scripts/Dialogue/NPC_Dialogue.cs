using UnityEngine;
using Unity.Cinemachine; // VCam 사용을 위해 추가
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// NPC에게 부착되어 대화 상호작용을 처리합니다.
/// IInteractable 인터페이스를 구현합니다.
/// 💥 [수정됨] GameEventManager의 스택에 따라 대화 분기 기능 추가
/// </summary>
public class NPC_Dialogue : MonoBehaviour, IInteractable
{
    [BoxGroup("Virtual Camera"), Required]
    [Tooltip("이 NPC와 대화할 때 활성화할 Cinemachine Virtual Camera")]
    [SerializeField] private CinemachineCamera virtualCamera;

    [Header("Default Dialogue")]
    [Tooltip("일반 NPC가 재생할 대화 데이터 목록 (Ending Branch가 false일 때 사용)")]
    [SerializeField] private List<DialogueData> dialogueList;

    [BoxGroup("Default Dialogue")]
    [Tooltip("true: 마지막 대화를 계속 반복 / false: 목록 처음으로 돌아감")]
    [SerializeField] private bool loopLastDialogue = true;

    // -------------------------------------------------------------------------
    [Header("Ending Branch (Optional)")]
    [Tooltip("true로 설정하면, 상호작용 시 GameEventManager의 endingStack을 확인하여 대화를 분기합니다.")]
    [SerializeField] private bool isEndingBranch = false;

    [Tooltip("Stack >= 2일 때 재생할 대화")]
    [ShowIf("isEndingBranch")] // isEndingBranch가 true일 때만 이 필드를 표시
    [SerializeField] private DialogueData dialogueForStackHigh;

    [Tooltip("Stack < 2일 때 재생할 대화")]
    [ShowIf("isEndingBranch")]
    [SerializeField] private DialogueData dialogueForStackLow;
    // -------------------------------------------------------------------------

    [BoxGroup("Visuals")]
    [Tooltip("플레이어가 범위에 들어왔을 때 활성화할 아웃라인 (선택 사항)")]
    [SerializeField] private GameObject outlineObject;

    private int _dialogueIndex = 0;
    private bool _isInteracting = false;
    private PlayerInteractor _currentPlayerInteractor;

    private DialogueData _currentDialogueData;

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
        if (_isInteracting) return;

        _isInteracting = true;
        _currentPlayerInteractor = interactor;

        PlayerController pc = interactor.GetPlayerController();

        // 1. 플레이어 조작 및 마우스 커서 상태 변경
        if (pc != null)
        {
            pc.SetUIMode(true); // 마우스 커서 표시 및 조작 잠금
        }
        interactor.LockInteraction(true); // "E" 키 연타 방지

        // 2. VCam 활성화 (Priority를 높여서)
        if (virtualCamera != null) virtualCamera.Priority = 100;

        // 💥 [핵심 수정] 💥
        // 3. 재생할 대화 데이터 결정
        if (isEndingBranch)
        {
            // 엔딩 분기 NPC일 경우
            if (GameEventManager.Instance == null)
            {
                Debug.LogError("isEndingBranch=true이지만 GameEventManager.Instance가 없습니다!");
                CleanupInteraction(interactor, pc); // 상호작용 즉시 종료
                return;
            }

            // GameEventManager의 스택을 확인하여 대화 분기
            if (GameEventManager.Instance.endingStack >= 2)
            {
                _currentDialogueData = dialogueForStackHigh;
            }
            else
            {
                _currentDialogueData = dialogueForStackLow;
            }
        }
        else
        {
            // 일반 NPC일 경우 (기존 로직)
            if (dialogueList.Count == 0)
            {
                Debug.LogWarning("대화 목록(dialogueList)이 비어있습니다.");
                CleanupInteraction(interactor, pc); // 상호작용 즉시 종료
                return;
            }

            _currentDialogueData = dialogueList[_dialogueIndex];

            // 대화 인덱스 관리 (엔딩 분기가 아닐 때만)
            if (_dialogueIndex < dialogueList.Count - 1)
            {
                _dialogueIndex++;
            }
            else if (!loopLastDialogue)
            {
                _dialogueIndex = 0;
            }
        }

        // 4. 결정된 대화 데이터가 유효한지 최종 확인
        if (_currentDialogueData == null)
        {
            Debug.LogError("재생할 _currentDialogueData가 null입니다! (Ending Branch 또는 dialogueList가 할당되었는지 확인)");
            CleanupInteraction(interactor, pc);
            return;
        }

        // 5. 대화 매니저에게 대화 시작 요청
        DialogueManager.Instance.StartDialogue(
            _currentDialogueData,
            virtualCamera, // 현재 NPC의 VCam을 매니저에게 전달
            OnDialogueFinished,
            OnChoiceMade
        );
    }

    /// <summary>
    /// DialogueManager에서 선택지가 선택되었을 때 호출하는 콜백 함수
    /// </summary>
    private void OnChoiceMade(DialogueData nextDialogue)
    {
        // 1. 선택지 결과에 따라 다음 대화 분기 처리
        if (nextDialogue != null)
        {
            // 💥 [중요] 
            // OnChoiceMade에서 nextDialogue가 넘어오면, 
            // _currentDialogueData를 '다음 대화'로 교체합니다.
            // (예: DA_EscapePod_Time -> DA_Ending_1_Capsule)
            _currentDialogueData = nextDialogue;

            // 다음 대화 시작 시 VCam Priority 100을 유지합니다.
            DialogueManager.Instance.StartDialogue(
                _currentDialogueData,
                virtualCamera, // VCam을 다시 전달
                OnDialogueFinished,
                OnChoiceMade
            );
        }
        else
        {
            // 선택지 결과가 null이면 대화를 완전히 종료합니다.
            // (선택지에 'Next Dialogue'가 할당되지 않은 경우)
            OnDialogueFinished();
        }
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
    /// DialogueManager가 대화를 끝냈을 때 호출하는 콜백 함수 (분기 처리 완료 후)
    /// </summary>
    private void OnDialogueFinished()
    {
        // 1. VCam 비활성화 (Priority 원래대로)
        if (virtualCamera != null) virtualCamera.Priority = 0;

        // 2. 플레이어 조작 및 마우스 커서 상태 복구
        if (_currentPlayerInteractor != null)
        {
            PlayerController pc = _currentPlayerInteractor.GetPlayerController();
            if (pc != null)
            {
                pc.SetUIMode(false); // 마우스 커서 숨김 및 조작 잠금 해제
            }
            _currentPlayerInteractor.LockInteraction(false);
        }

        // 3. 💥 [엔딩 트리거 확인] 💥
        // (예: DA_Ending_1_Capsule 대화가 끝났을 때)
        if (_currentDialogueData != null && _currentDialogueData.isEndingTrigger)
        {
            if (GameEventManager.Instance != null)
            {
                // GameEventManager에게 스택에 맞는 엔딩 씬 로드를 요청합니다.
                GameEventManager.Instance.LoadEndingScene();
            }
            else
            {
                Debug.LogWarning("GameEventManager.Instance가 씬에 없어 엔딩을 로드할 수 없습니다!");
            }
        }

        // 4. 모든 상태 리셋
        _isInteracting = false;
        _currentPlayerInteractor = null;
        _currentDialogueData = null;
    }

    /// <summary>
    /// [HELPER] 상호작용이 시작조차 못하고 실패했을 때(예:데이터 없음)
    /// 플레이어와 VCam 상태를 즉시 원상 복구합니다.
    /// </summary>
    private void CleanupInteraction(PlayerInteractor interactor, PlayerController pc)
    {
        if (virtualCamera != null) virtualCamera.Priority = 0;

        if (pc != null)
        {
            pc.SetUIMode(false);
        }
        if (interactor != null)
        {
            interactor.LockInteraction(false);
        }

        _isInteracting = false;
        _currentPlayerInteractor = null;
        _currentDialogueData = null;
    }
}