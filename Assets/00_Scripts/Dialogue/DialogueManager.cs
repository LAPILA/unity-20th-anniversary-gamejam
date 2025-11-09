using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using DG.Tweening;
using System;
using Unity.Cinemachine; // VCam 처리를 위해 추가

/// <summary>
/// 범용 대화 시스템을 관리하는 싱글톤 매니저 (DOTween 코드 기반)
/// - 페르소나 스타일 (L/R 슬라이드)
/// - 다국어, 텍스트 타이핑, 종료 콜백 지원
/// </summary>
public class DialogueManager : MonoBehaviour
{
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

    // 사용자가 추가 요청한 대화창 배경 이미지 컴포넌트
    [BoxGroup("UI References"), Required]
    [SerializeField] private Image DialogueBox_Image;

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
    private bool _isBusy = false; // 슬라이드, 선택지, 타이핑 중인지
    private Language _currentLanguage = Language.Korean; // 임시
    private Coroutine _typingCoroutine;
    private DialogueLine _previousLine;
    private Action _onDialogueComplete;

    // 선택지 처리를 위한 Action (NPC_Dialogue로 결과를 전달)
    private Action<DialogueData> _onChoiceSelected;
    private CinemachineCamera _currentVCamOverride;

    public bool IsDialogueActive => _isBusy || _isTyping || dialogueBoxContainer.activeSelf || dimmingPanel.gameObject.activeSelf;

    // 임시 언어 설정
    public enum Language { Korean, English }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            _lineQueue = new Queue<DialogueLine>();
            _dialogueBoxRect = dialogueBoxContainer.GetComponent<RectTransform>();

            // DOTween 초기화 및 기본 위치 설정 (깜빡임 최소화)
            _dialogueBoxRect.anchoredPosition = new Vector2(0, offscreenY);

            // 선택지 및 어둡게 패널 초기화 (비활성화 및 투명화)
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
        // 모든 텍스트, 초상화, 배경 이미지를 투명하게 초기화합니다.
        speakerNameText.text = "";
        dialogueText.text = "";

        portraitImage.sprite = null;
        portraitImage.color = Color.clear;

        DialogueBox_Image.sprite = null;
        DialogueBox_Image.color = Color.clear;
    }

    public void StartDialogue(DialogueData data, Action onCompleteCallback = null, Action<DialogueData> onChoiceSelected = null)
    {
        if (_isBusy) return;

        _lineQueue.Clear();
        foreach (var line in data.lines)
        {
            _lineQueue.Enqueue(line);
        }

        _onDialogueComplete = onCompleteCallback;
        _onChoiceSelected = onChoiceSelected; // 선택지 결과 처리 콜백 저장

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
                ShowNextLine(); // 5. 슬라이드 완료 후 첫 대사 표시
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
            // 현재 대사가 선택지 분기점이었다면, 클릭을 무시하고 선택지를 기다립니다.
            if (_previousLine.isChoicePoint) return;

            ShowNextLine();
        }
    }

    private void ShowNextLine()
    {
        if (_isBusy) return;

        // 💥💥 선택지 표시 로직 💥💥
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
            // 이전 VCam이 있다면 우선순위를 낮춥니다.
            if (_currentVCamOverride != null) _currentVCamOverride.Priority = 0;

            // 새 VCam을 활성화하고 현재 VCam으로 저장합니다.
            line.overrideVCam.Priority = 101;
            _currentVCamOverride = line.overrideVCam;
        }
        // VCam이 null이고 이전에 활성화된 VCam이 있다면, 우선순위를 낮춥니다.
        else if (_currentVCamOverride != null)
        {
            _currentVCamOverride.Priority = 0;
            _currentVCamOverride = null;
        }

        // 사운드 재생
        if (line.dialogueSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(line.dialogueSound);
        }

        // 화면 흔들림 (Screen Shake) - Cinemachine Impulse 등 구현 필요 (간단 구현은 생략)
        // [TODO: Cinemachine Impulse Source를 사용하여 화면 흔들림 구현]
        if (line.cameraShakeIntensity > 0.0f)
        {
            Debug.Log($"[Effect] Camera shake: {line.cameraShakeIntensity}");
            // 예시: Cinemachine Brain 또는 Impulse Source를 통해 흔들림을 트리거합니다.
        }
    }

    private void ClearDynamicEvents()
    {
        // 오버라이드 VCam 원상 복구 (VCam Priority 100이 NPC_Dialogue의 기본 VCam)
        if (_currentVCamOverride != null)
        {
            _currentVCamOverride.Priority = 0;
            _currentVCamOverride = null;
        }
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
        }
        else
        {
            portraitImage.sprite = null;
            portraitImage.color = Color.clear;
            DialogueBox_Image.color = Color.clear;
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

        button.onClick.AddListener(() => OnChoiceMade(option.nextDialogue));
    }

    private void OnChoiceMade(DialogueData nextDialogue)
    {
        _isBusy = true; // 선택지 처리 중 잠금

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
        // DOTween의 Typing Effect와 달리, 이 코루틴은 다음 대사 클릭 시 StopCoroutine으로 종료되어야 합니다.
        while (charIndex < fullText.Length)
        {
            dialogueText.maxVisibleCharacters++;
            charIndex++;
            yield return new WaitForSeconds(typeSpeed);
        }

        _isTyping = false;
        // 타이핑이 완전히 끝났을 때만 선택지를 표시하도록 ShowNextLine 호출 (선택지 대기)
        if (_previousLine.isChoicePoint)
        {
            _isBusy = false; // 타이핑은 끝났으므로 클릭 가능하게 락 해제
            ShowNextLine(); // ShowChoices를 호출
        }
    }

    private void EndDialogue()
    {
        if (_isBusy) return;
        _isBusy = true;
        _previousLine = default;

        ClearDynamicEvents(); // 동적 VCam 원상 복구

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