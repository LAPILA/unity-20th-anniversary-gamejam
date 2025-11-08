using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

/// <summary>
/// 플레이어에게 부착되어 "Interact" 입력을 감지하고
/// 범위 내의 IInteractable 오브젝트와 상호작용합니다.
/// </summary>
[RequireComponent(typeof(PlayerInput), typeof(PlayerController))]
public class PlayerInteractor : MonoBehaviour
{
    [BoxGroup("References"), Required]
    [Tooltip("플레이어의 메인 PlayerInput 컴포넌트")]
    [SerializeField] private PlayerInput playerInput;

    [BoxGroup("References"), Required]
    [Tooltip("플레이어의 PlayerController (이동 잠금용)")]
    [SerializeField] private PlayerController playerController;

    [BoxGroup("UI")]
    [Tooltip("상호작용 가능할 때 띄울 UI (예: 'E' 키 프롬프트)")]
    [SerializeField] private GameObject interactPromptUI;

    private IInteractable _currentInteractable;
    private bool _canInteract = true;

    void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
    }

    void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            playerInput.actions["Interact"].performed += OnInteractPerformed;
        }
    }

    void OnDisable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            playerInput.actions["Interact"].performed -= OnInteractPerformed;
        }
    }

    /// <summary>
    /// "Interact" 키가 눌렸을 때
    /// </summary>
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        // ▼▼▼ [문제 2 해결] 입력 라우팅 로직 ▼▼▼

        // 1. DialogueManager가 활성화(대화 중) 상태인지 확인합니다.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            // 2. 대화 중이라면, "Interact" 키는 "다음 줄"을 의미합니다.
            DialogueManager.Instance.OnClickNext();
        }
        // 3. 대화 중이 아니고, 상호작용이 가능하며, 대상이 있다면 "상호작용 시작"을 의미합니다.
        else if (_canInteract && _currentInteractable != null)
        {
            _currentInteractable.Interact(this);
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }

    // 플레이어의 '상호작용 범위' 콜라이더(Trigger)에 의해 호출됨
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            _currentInteractable = interactable;
            _currentInteractable.OnPlayerEnterRange();

            // 대화 중이 아닐 때만 프롬프트 표시
            if (_canInteract && interactPromptUI != null)
            {
                interactPromptUI.SetActive(true);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out IInteractable interactable) && interactable == _currentInteractable)
        {
            _currentInteractable.OnPlayerExitRange();
            _currentInteractable = null;
            if (interactPromptUI != null) interactPromptUI.SetActive(false);
        }
    }

    /// <summary>
    /// 대화 중일 때처럼 플레이어의 상호작용 입력을 잠급니다.
    /// </summary>
    public void LockInteraction(bool isLocked)
    {
        _canInteract = !isLocked;
        // 상호작용이 잠기면 프롬프트도 숨김
        if (isLocked && interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }
    }

    /// <summary>
    /// PlayerController 참조를 반환합니다.
    /// </summary>
    public PlayerController GetPlayerController()
    {
        return playerController;
    }
}