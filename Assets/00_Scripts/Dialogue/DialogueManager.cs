using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using DG.Tweening;
using System;

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
    private bool _isBusy = false; // 슬라이드 또는 타이핑 중인지
    private Language _currentLanguage = Language.Korean; // 임시
    private Coroutine _typingCoroutine;
    private DialogueLine _previousLine;
    private Action _onDialogueComplete;

    public bool IsDialogueActive => _isBusy || _isTyping || dialogueBoxContainer.activeSelf;

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
            ClearDialogueUI(); // Awake에서 UI 상태를 초기화하여 투명하게 시작
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Start에서는 별도의 동작이 필요 없습니다.
    }

    private void ClearDialogueUI()
    {
        // 모든 텍스트, 초상화, 배경 이미지를 투명하게 초기화합니다.
        speakerNameText.text = "";
        dialogueText.text = "";

        portraitImage.sprite = null;
        portraitImage.color = Color.clear;

        DialogueBox_Image.sprite = null; // 배경 이미지도 투명화 (만약 Sprite 모드라면)
        DialogueBox_Image.color = Color.clear; // 배경 이미지 컬러도 투명화
    }

    public void StartDialogue(DialogueData data, Action onCompleteCallback = null)
    {
        if (_isBusy) return;

        _lineQueue.Clear();
        foreach (var line in data.lines)
        {
            _lineQueue.Enqueue(line);
        }

        _onDialogueComplete = onCompleteCallback;

        // 1. UI를 투명하게 초기화합니다.
        ClearDialogueUI();

        // 2. 대화창을 화면 밖 위치로 즉시 고정합니다. (깜빡임 2차 방지)
        _dialogueBoxRect.anchoredPosition = new Vector2(_dialogueBoxRect.anchoredPosition.x, offscreenY);

        // 3. 켜기를 수행합니다. (ClearDialogueUI가 이미 투명하게 만들었으므로 깜빡임 최소화)
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
            ShowNextLine();
        }
    }

    private void ShowNextLine()
    {
        if (_isBusy) return;

        if (_lineQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine currentLine = _lineQueue.Dequeue();

        // 화자가 같고, 대화창이 이미 활성화되어 있으면(첫 대사가 아니면)
        if (dialogueBoxContainer.activeSelf && currentLine.speakerName == _previousLine.speakerName)
        {
            // 슬라이드 없이 텍스트만 갱신
            StartCoroutine(UpdateTextOnlyRoutine(currentLine));
        }
        else
        {
            // 화자가 다르면 L/R 슬라이드 실행
            StartCoroutine(ShowLineRoutine(currentLine));
        }

        _previousLine = currentLine;
    }

    /// <summary>
    /// L/R 슬라이드 없이 텍스트만 갱신하는 코루틴
    /// </summary>
    private IEnumerator UpdateTextOnlyRoutine(DialogueLine line)
    {
        // 1. 기존 타이핑 중지
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _isTyping = false;
        }

        // 2. 초상화 갱신
        UpdatePortraitAndDialogueBox(line);

        string textToShow = (_currentLanguage == Language.Korean)
            ? line.text_Korean
            : line.text_English;

        // 3. 새 타이핑 시작
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

            // 슬라이드 아웃 완료 후, 다음 슬라이드 인 애니메이션 전에 대화창 이미지 비활성화/투명화
            ClearDialogueUI();
        }

        // 1. 이름, 초상화, 배경 설정 (슬라이드 인 애니메이션 전에 배치)
        // 이 시점에서는 텍스트 가시성이 0이고, 초상화/배경은 UpdatePortraitAndDialogueBox에서 처리됨
        speakerNameText.text = line.speakerName;
        UpdatePortraitAndDialogueBox(line);

        // 2. 텍스트 설정 및 즉시 숨김 (가장 중요)
        dialogueText.text = textToShow;
        dialogueText.maxVisibleCharacters = 0;

        // 3. 새 시작 위치 설정 및 슬라이드 인 시작
        float startX = line.isPlayer ?
            -_dialogueBoxRect.rect.width * 2f : _dialogueBoxRect.rect.width * 2f;

        _dialogueBoxRect.anchoredPosition = new Vector2(startX, onscreenY);

        _dialogueBoxRect.DOAnchorPosX(0, slideDuration).SetEase(Ease.OutCubic);
        yield return new WaitForSeconds(slideDuration);

        _isBusy = false; // 이제 타이핑 시작
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypewriterEffect(textToShow));
    }

    /// <summary>
    /// 초상화 및 대화창 배경 이미지 갱신 로직을 통합
    /// </summary>
    private void UpdatePortraitAndDialogueBox(DialogueLine line)
    {
        // 초상화 갱신
        if (line.portrait != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.color = Color.white;
        }
        else
        {
            portraitImage.sprite = null;
            portraitImage.color = Color.clear;
        }

        // 대화창 이미지 갱신
        // DialogueBox_Image는 항상 투명하거나,
        // 플레이어/NPC에 따라 다른 스타일을 가질 수 있습니다.
        // 여기서는 간단히 투명/불투명만 제어합니다.

        // 초상화가 있을 때만 배경 이미지도 보이도록 설정 (일반적인 방식)
        if (line.portrait != null)
        {
            // 실제 배경 이미지를 설정하거나, 단순히 불투명하게 만듭니다.
            DialogueBox_Image.color = Color.white; // 배경이 보이도록 불투명하게 설정
        }
        else
        {
            // 초상화가 없으면 배경도 숨김
            DialogueBox_Image.color = Color.clear;
        }
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
        while (charIndex < fullText.Length)
        {
            dialogueText.maxVisibleCharacters++;
            charIndex++;
            yield return new WaitForSeconds(typeSpeed);
        }

        _isTyping = false;
    }

    private void EndDialogue()
    {
        if (_isBusy) return;
        _isBusy = true;
        _previousLine = default; // 다음 대화가 슬라이드인으로 시작하도록 초기화

        _dialogueBoxRect.DOAnchorPosY(offscreenY, slideDuration * 1.5f)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                dialogueBoxContainer.SetActive(false);
                _isBusy = false;

                // 대화 종료 시 모든 UI를 투명하게 초기화
                ClearDialogueUI();

                _onDialogueComplete?.Invoke();
                _onDialogueComplete = null;
            });
    }
}