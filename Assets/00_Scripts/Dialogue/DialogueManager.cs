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

    [BoxGroup("Text Settings")]
    [Tooltip("텍스트 타이핑 속도")]
    [SerializeField] private float typeSpeed = 0.03f;

    // (3프레임 애니메이션 관련 필드 제거됨)

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
    // (3프레임 애니메이션 코루틴 제거됨)
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

            // [수정됨]
            // Awake()에서 SetActive(false)를 호출하는 대신,
            // 유니티 에디터에서 dialogueBoxContainer를
            // 기본으로 '비활성화(Checked OFF)' 해두는 것을 권장합니다.
            // 이 스크립트가 StartDialogue에서 켜고 EndDialogue에서 끕니다.
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 시작 시 대화창을 화면 밖으로 설정 (Awake에서 이미 비활성화됨)
        _dialogueBoxRect.anchoredPosition = new Vector2(0, offscreenY);
    }

    private void ClearDialogueUI()
    {
        speakerNameText.text = "";
        dialogueText.text = "";
        portraitImage.sprite = null;
        portraitImage.color = Color.clear;
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

        // ▼▼▼ [수정됨] 실행 순서 변경 ▼▼▼

        // 1. 켜기를 먼저 수행합니다.
        dialogueBoxContainer.SetActive(true);

        // 2. 켜진 직후에 UI를 즉시 청소합니다.
        // (이 시점에서 컴포넌트들이 활성화되어 .text/.sprite 변경이 즉시 반영됩니다)
        ClearDialogueUI();

        // 3. 비어있는 대화창을 위로 슬라이드합니다.
        _isBusy = true;
        _dialogueBoxRect.DOAnchorPosY(onscreenY, slideDuration * 1.5f)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                _isBusy = false;
                ShowNextLine(); // 4. 슬라이드 완료 후 첫 대사 표시
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

        // ▼▼▼ [문제 2 해결] 동일 화자 체크 ▼▼▼
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
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        _previousLine = currentLine;
    }

    /// <summary>
    /// [문제 2 해결] L/R 슬라이드 없이 텍스트만 갱신하는 코루틴
    /// </summary>
    private IEnumerator UpdateTextOnlyRoutine(DialogueLine line)
    {
        // 1. 기존 타이핑 중지
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _isTyping = false;
        }

        // 2. 텍스트/초상화 갱신 (화자 이름은 동일하므로 생략)
        // (초상화가 바뀔 수 있으니 갱신)
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

        string textToShow = (_currentLanguage == Language.Korean)
            ? line.text_Korean
            : line.text_English;

        // 3. 새 타이핑 시작
        _typingCoroutine = StartCoroutine(TypewriterEffect(textToShow));

        yield break; // 코루틴 즉시 종료 (TypewriterEffect가 _isTyping을 관리)
    }

    private IEnumerator ShowLineRoutine(DialogueLine line)
    {
        _isBusy = true;

        float exitX = line.isPlayer ?
            _dialogueBoxRect.rect.width * 2f : -_dialogueBoxRect.rect.width * 2f;

        // _previousLine.speakerName이 null이 아니면(첫 대사가 아니면) 슬라이드 아웃 실행
        if (!string.IsNullOrEmpty(_previousLine.speakerName))
        {
            yield return _dialogueBoxRect.DOAnchorPosX(exitX, slideDuration)
               .SetEase(Ease.InCubic)
               .WaitForCompletion();
        }

        speakerNameText.text = line.speakerName;

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

        string textToShow = (_currentLanguage == Language.Korean)
            ? line.text_Korean
            : line.text_English;
        dialogueText.text = textToShow;
        dialogueText.maxVisibleCharacters = 0;

        float startX = line.isPlayer ?
            -_dialogueBoxRect.rect.width * 2f : _dialogueBoxRect.rect.width * 2f;
        _dialogueBoxRect.anchoredPosition = new Vector2(startX, onscreenY);

        _dialogueBoxRect.DOAnchorPosX(0, slideDuration).SetEase(Ease.OutCubic);
        yield return new WaitForSeconds(slideDuration);

        _isBusy = false; // 이제 타이핑 시작
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypewriterEffect(textToShow));
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

                _onDialogueComplete?.Invoke();
                _onDialogueComplete = null;
            });
    }
}