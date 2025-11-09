using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using DG.Tweening;
using System;
using Unity.Cinemachine;

/// <summary>
/// 범용 대화 시스템을 관리하는 싱글톤 매니저 (DOTween 코드 기반)
/// - 페르소나 스타일 (L/R 슬라이드)
/// - 다국어, 텍스트 타이핑, 종료 콜백 지원
/// </summary>
public class DialogueManager : MonoBehaviour
{
    // ... (모든 변수와 Awake, ClearDialogueUI, StartDialogue, OnClickNext, ShowNextLine - 변경 없음) ...
    // ... (ExecuteDynamicEvents, ClearDynamicEvents, UpdateTextOnlyRoutine, ShowLineRoutine - 변경 없음) ...
    // ... (UpdatePortraitAndDialogueBox, ShowChoices, CreateChoiceButton - 변경 없음) ...

    public static DialogueManager Instance { get; private set; }

    [BoxGroup("UI References"), Required]
    [Tooltip("대화창 전체 컨테이너 (RectTransform 제어 대상)")]
    [SerializeField] private GameObject dialogueBoxContainer;

    [BoxGroup("UI References"), Required]
    [SerializeField] private TextMeshProUGUI speakerNameText;

    [BoxGroup("UI References"), Required]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [BoxGroup("UI References"), Required]
    [SerializeField] private Image portraitImage;

    [BoxGroup("UI References"), Required]
    [SerializeField] private Image DialogueBox_Image;

    [BoxGroup("UI References"), Required]
    [SerializeField] private Image NameBox_Image;

    [BoxGroup("Choice UI"), Required]
    [Tooltip("화면 전체를 덮을 검은색 반투명 배경 이미지")]
    [SerializeField] private Image dimmingPanel;

    [BoxGroup("Choice UI"), Required]
    [Tooltip("선택지 버튼들이 담길 컨테이너")]
    [SerializeField] private GameObject choiceContainer;

    [BoxGroup("Choice UI"), Required]
    [Tooltip("선택지 버튼 프리팹 (TMP_Button)")]
    [SerializeField] private Button choiceButtonPrefab;

    [BoxGroup("Audio")]
    [Tooltip("사운드 재생용 AudioSource")]
    [SerializeField] private AudioSource audioSource;

    [BoxGroup("Audio")]
    [Tooltip("언더테일 스타일 텍스트 타이핑 기본 사운드 클립")]
    [SerializeField] private AudioClip defaultTypingSound;

    [BoxGroup("Text Settings")]
    [Tooltip("텍스트 타이핑 속도")]
    [SerializeField] private float typeSpeed = 0.03f;

    [BoxGroup("Animation (Slide-In)")]
    [Tooltip("L/R 슬라이드 인에 걸리는 시간")]
    [SerializeField] private float slideDuration = 0.4f;

    [BoxGroup("Animation (Slide-In)")]
    [Tooltip("대화창이 화면 밖으로 나갈 때 Y 위치")]
    [SerializeField] private float offscreenY = -1000f;

    [BoxGroup("Animation (Slide-In)")]
    [Tooltip("대화창이 화면 안으로 들어왔을 때 Y 위치")]
    [SerializeField] private float onscreenY = 0f;

    // 내부 상태
    private RectTransform _dialogueBoxRect;
    private Queue<DialogueLine> _lineQueue;
    private bool _isTyping = false;
    private bool _isBusy = false;
    private Language _currentLanguage = Language.Korean;
    private Coroutine _typingCoroutine;
    private DialogueLine _previousLine;
    private Action _onDialogueComplete;

    private Action<DialogueData> _onChoiceSelected;
    private CinemachineCamera _currentVCamOverride;
    private CinemachineCamera _baseVCam; // 💥 [추가] NPC로부터 기본 VCam을 저장할 변수

    // 현재 대사에 설정된 사운드 클립 (타이핑 코루틴에서 사용)
    private AudioClip _currentLineTypingSound;

    public bool IsDialogueActive => _isBusy || _isTyping || dialogueBoxContainer.activeSelf || dimmingPanel.gameObject.activeSelf;

    public enum Language { Korean, English }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            _lineQueue = new Queue<DialogueLine>();
            _dialogueBoxRect = dialogueBoxContainer.GetComponent<RectTransform>();

            _dialogueBoxRect.anchoredPosition = new Vector2(0, offscreenY);

            dimmingPanel.gameObject.SetActive(false);
            dimmingPanel.color = new Color(0, 0, 0, 0);
            choiceContainer.SetActive(false);

            ClearDialogueUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void ClearDialogueUI()
    {
        speakerNameText.text = "";
        dialogueText.text = "";

        portraitImage.sprite = null;
        portraitImage.color = Color.clear;

        DialogueBox_Image.color = Color.clear;

        NameBox_Image.color = Color.clear;
    }

    public void StartDialogue(DialogueData data, CinemachineCamera baseVCam, Action onCompleteCallback = null, Action<DialogueData> onChoiceSelected = null)
    {
        if (_isBusy) return;

        _lineQueue.Clear();
        foreach (var line in data.lines)
        {
            _lineQueue.Enqueue(line);
        }

        _onDialogueComplete = onCompleteCallback;
        _onChoiceSelected = onChoiceSelected;
        _baseVCam = baseVCam; // 💥 [추가] 기본 VCam 저장

        ClearDynamicEvents(); // 새 대화 시작 전 이전 동적 VCam 초기화

        // 💥 [수정됨] StartDialogue가 호출될 때도 _previousLine을 초기화합니다.
        // 이것이 OnChoiceMade에서 재시작될 때 선택지가 다시 뜨는 것을 막는 2차 방어선입니다.
        _previousLine = default;

        // 1. UI를 투명하게 초기화합니다.
        ClearDialogueUI();

        // 2. 대화창을 화면 밖 위치로 즉시 고정합니다.
        _dialogueBoxRect.anchoredPosition = new Vector2(_dialogueBoxRect.anchoredPosition.x, offscreenY);

        // 3. 켜기를 수행합니다.
        dialogueBoxContainer.SetActive(true);

        // 4. 비어있는 대화창을 위로 슬라이드합니다.
        _isBusy = true;
        _dialogueBoxRect.DOAnchorPosY(onscreenY, slideDuration * 1.5f)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                _isBusy = false;
                ShowNextLine();
            });
    }

    public void OnClickNext()
    {
        if (_isBusy) return;

        if (_isTyping)
        {
            CompleteTyping();
        }
        else
        {
            // 💥 [수정됨] 타이핑이 끝났고, 선택지 대기 상태(isChoicePoint)라면
            // ShowNextLine()을 호출하여 선택지를 띄웁니다.
            // (이전에는 return; 으로 막혀있었음)
            if (_previousLine.isChoicePoint)
            {
                ShowNextLine();
                return;
            }

            ShowNextLine();
        }
    }

    private void ShowNextLine()
    {
        if (_isBusy) return;

        // 타이핑이 완료된 후 (또는 OnClickNext로 호출된 후), 
        // 이전에 처리하지 못한 선택지 분기가 있다면 처리
        if (_previousLine.isChoicePoint)
        {
            ShowChoices(_previousLine.choice1, _previousLine.choice2);
            return;
        }

        if (_lineQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine currentLine = _lineQueue.Dequeue();

        // 화자가 같고, 대화창이 이미 활성화되어 있으면
        if (dialogueBoxContainer.activeSelf && currentLine.speakerName == _previousLine.speakerName)
        {
            StartCoroutine(UpdateTextOnlyRoutine(currentLine));
        }
        else
        {
            // 화자가 다르면 L/R 슬라이드 실행
            StartCoroutine(ShowLineRoutine(currentLine));
        }

        _previousLine = currentLine;
    }

    private void ExecuteDynamicEvents(DialogueLine line)
    {
        // VCam 오버라이드
        if (line.overrideVCam != null)
        {
            // VCam이 null이 아니면 기존 VCam(101)보다 높게 설정하여 오버라이드
            if (_currentVCamOverride != null) _currentVCamOverride.Priority = 0;

            line.overrideVCam.Priority = 102; // NPC_Dialogue의 VCam(100)보다 높게
            _currentVCamOverride = line.overrideVCam;
        }
        // 오버라이드 VCam이 없고, 이전에 오버라이드된 VCam이 있다면 해제
        else if (_currentVCamOverride != null)
        {
            _currentVCamOverride.Priority = 0;
            _currentVCamOverride = null;
        }

        // 사운드 클립 설정 (타이핑 코루틴에서 사용)
        _currentLineTypingSound = line.dialogueSound != null ? line.dialogueSound : defaultTypingSound;

        // 화면 흔들림 (간단한 화면 흔들림 효과)
        if (line.cameraShakeIntensity > 0.0f)
        {
            // 💥 [수정] Camera.main 대신 활성화된 VCam을 흔듭니다.
            CinemachineCamera activeVCam = _currentVCamOverride != null ? _currentVCamOverride : _baseVCam;
            if (activeVCam != null)
            {
                // VCam의 트랜스폼 회전을 흔들면 CinemachineBrain이 이를 반영합니다.
                activeVCam.transform.DOShakeRotation(
                    0.3f, // 흔들림 시간
                    line.cameraShakeIntensity * 3f, // 흔들림 강도 (회전이므로 값을 좀 더 줌)
                    10 // 자글거림
                );
            }
        }
    }

    private void ClearDynamicEvents()
    {
        // 오버라이드 VCam 원상 복구
        if (_currentVCamOverride != null)
        {
            _currentVCamOverride.Priority = 0;
            _currentVCamOverride = null;
        }
        // 타이핑 사운드 초기화
        _currentLineTypingSound = null;
    }

    /// <summary>
    /// L/R 슬라이드 없이 텍스트만 갱신하는 코루틴
    /// </summary>
    private IEnumerator UpdateTextOnlyRoutine(DialogueLine line)
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _isTyping = false;
        }

        ExecuteDynamicEvents(line); // 동적 이벤트 실행

        UpdatePortraitAndDialogueBox(line);

        string textToShow = (_currentLanguage == Language.Korean)
            ? line.text_Korean
            : line.text_English;

        _typingCoroutine = StartCoroutine(TypewriterEffect(textToShow));

        yield break;
    }

    private IEnumerator ShowLineRoutine(DialogueLine line)
    {
        _isBusy = true;

        string textToShow = (_currentLanguage == Language.Korean)
            ? line.text_Korean
            : line.text_English;

        float exitX = line.isPlayer ?
            _dialogueBoxRect.rect.width * 2f : -_dialogueBoxRect.rect.width * 2f;

        // 이전 대화 슬라이드 아웃
        if (!string.IsNullOrEmpty(_previousLine.speakerName))
        {
            yield return _dialogueBoxRect.DOAnchorPosX(exitX, slideDuration)
                .SetEase(Ease.InCubic)
                .WaitForCompletion();

            ClearDialogueUI();
        }

        // 1. 이름, 초상화, 배경 설정
        speakerNameText.text = line.speakerName;
        UpdatePortraitAndDialogueBox(line);

        // 2. 텍스트 설정 및 즉시 숨김
        dialogueText.text = textToShow;
        dialogueText.maxVisibleCharacters = 0;

        // 3. 새 시작 위치 설정 및 슬라이드 인 시작
        float startX = line.isPlayer ?
            -_dialogueBoxRect.rect.width * 2f : _dialogueBoxRect.rect.width * 2f;

        _dialogueBoxRect.anchoredPosition = new Vector2(startX, onscreenY);

        _dialogueBoxRect.DOAnchorPosX(0, slideDuration).SetEase(Ease.OutCubic);
        yield return new WaitForSeconds(slideDuration);

        ExecuteDynamicEvents(line); // 슬라이드 인 완료 후 동적 이벤트 실행

        _isBusy = false;
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypewriterEffect(textToShow));
    }

    /// <summary>
    /// 초상화 및 대화창 배경 이미지 갱신 로직을 통합
    /// </summary>
    private void UpdatePortraitAndDialogueBox(DialogueLine line)
    {
        if (line.portrait != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.color = Color.white;
            DialogueBox_Image.color = Color.white;
            NameBox_Image.color = Color.white;
        }
        else
        {
            portraitImage.sprite = null;
            portraitImage.color = Color.clear;
            DialogueBox_Image.color = Color.clear;
            NameBox_Image.color = Color.clear;
        }
    }

    // --- 선택지 UI 로직 ---
    private void ShowChoices(ChoiceOption c1, ChoiceOption c2)
    {
        _isBusy = true;

        // 1. 검은색 반투명 배경 활성화 및 애니메이션
        dimmingPanel.gameObject.SetActive(true);
        dimmingPanel.DOColor(new Color(0, 0, 0, 0.5f), 0.3f);

        // 2. 기존 버튼 제거 및 선택지 컨테이너 활성화
        foreach (Transform child in choiceContainer.transform)
        {
            Destroy(child.gameObject);
        }
        choiceContainer.SetActive(true);

        // 3. 선택지 버튼 생성 및 설정
        CreateChoiceButton(c1);
        CreateChoiceButton(c2);
    }

    private void CreateChoiceButton(ChoiceOption option)
    {
        Button button = Instantiate(choiceButtonPrefab, choiceContainer.transform);
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

        string text = (_currentLanguage == Language.Korean) ? option.buttonText_Korean : option.buttonText_English;
        buttonText.text = text;

        // 💥 [핵심 수정] 💥
        // 버튼 클릭 리스너를 설정합니다.
        button.onClick.AddListener(() =>
        {
            // 1. 엔딩 스택 추가 (GameEventManager가 있는지 확인)
            if (GameEventManager.Instance != null && option.endingStackToAdd != 0)
            {
                GameEventManager.Instance.AddEndingStack(option.endingStackToAdd);
            }
            else if (GameEventManager.Instance == null && option.endingStackToAdd != 0)
            {
                // GameEventManager가 없으면 스택 추가가 안된다고 경고
                Debug.LogWarning("GameEventManager가 씬에 없어 엔딩 스택을 추가할 수 없습니다.");
            }

            // 2. 기존 선택지 로직 실행 (다음 대화로 넘어가거나 종료)
            OnChoiceMade(option.nextDialogue);
        });
    }

    private void OnChoiceMade(DialogueData nextDialogue)
    {
        _isBusy = true;

        // 💥💥 [핵심 수정] 💥💥
        // 선택지가 선택되면 _previousLine을 즉시 초기화(default)합니다.
        // 이것이 없으면 NPC_Dialogue가 StartDialogue를 다시 호출했을 때
        // ShowNextLine()이 _previousLine.isChoicePoint를 true로 읽어
        // 선택지 UI가 다시 뜨는 버그가 발생합니다.
        _previousLine = default;

        // 1. 선택지 UI 숨김
        dimmingPanel.DOColor(new Color(0, 0, 0, 0), 0.3f)
            .OnComplete(() => dimmingPanel.gameObject.SetActive(false));

        choiceContainer.SetActive(false);

        // 2. 대화창 슬라이드 아웃
        _dialogueBoxRect.DOAnchorPosY(offscreenY, slideDuration * 1.5f)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                dialogueBoxContainer.SetActive(false);
                ClearDialogueUI();

                _isBusy = false;

                // 3. NPC_Dialogue에게 다음 대화 데이터 전달
                _onChoiceSelected?.Invoke(nextDialogue);
                _onChoiceSelected = null;
            });
    }

    private void CompleteTyping()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
        }
        if (dialogueText.textInfo != null)
        {
            dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
        }
        _isTyping = false;
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        _isTyping = true;
        dialogueText.text = fullText;
        dialogueText.maxVisibleCharacters = 0;

        int charIndex = 0;

        // 언더테일 스타일 타이핑 사운드 재생
        float lastSoundTime = Time.time;
        float soundInterval = typeSpeed; // 타이핑 속도와 동일하게 설정 (튜닝 가능)

        while (charIndex < fullText.Length)
        {
            dialogueText.maxVisibleCharacters++;
            charIndex++;

            // 사운드 재생
            if (_currentLineTypingSound != null && audioSource != null && Time.time - lastSoundTime >= soundInterval)
            {
                // 💥 [수정] audioSource.Stop()을 제거해야 소리가 정상적으로 재생됩니다.
                // audioSource.Stop(); 
                audioSource.PlayOneShot(_currentLineTypingSound);
                lastSoundTime = Time.time;
            }

            yield return new WaitForSeconds(typeSpeed);
        }

        _isTyping = false;

        // 💥 [수정됨] 타이핑이 완료되었을 때 선택지 대기 상태라면
        // _isBusy만 false로 풀어주고, 클릭(OnClickNext)을 기다립니다.
        if (_previousLine.isChoicePoint)
        {
            _isBusy = false;
            // ShowNextLine()을 여기서 호출하면 클릭 없이 바로 선택지가 뜹니다.
            // 사용자가 "다음"을 눌러야 선택지가 뜨게 하려면 이 라인을 주석 처리합니다.
            // ShowNextLine(); // <- 주석 처리 시 클릭해야 선택지 나옴
        }
    }

    private void EndDialogue()
    {
        if (_isBusy) return;
        _isBusy = true;
        _previousLine = default;
        _baseVCam = null; // 💥 [추가] 기본 VCam 참조 해제

        ClearDynamicEvents(); // 동적 VCam/사운드 초기화

        _dialogueBoxRect.DOAnchorPosY(offscreenY, slideDuration * 1.5f)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                dialogueBoxContainer.SetActive(false);
                _isBusy = false;

                ClearDialogueUI();

                _onDialogueComplete?.Invoke();
                _onDialogueComplete = null;
            });
    }
}