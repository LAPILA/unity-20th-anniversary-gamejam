using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class SpriteCutsceneAnimator : MonoBehaviour
{
    // --- Panel Animation Data Class ---
    [System.Serializable]
    public class PanelAnimation
    {
        public GameObject targetPanel;
        public float duration = 1.0f;

        [Header("Sound Settings")]
        public AudioClip panelSound;
        [Range(0f, 1f)]
        public float panelSoundVolume = 1.0f;

        [Header("Fade Settings")]
        public bool doFade = true;
        public float startAlpha = 0f;
        public float endAlpha = 1f;

        [Header("Movement Settings")]
        public bool doMove = false;
        public Vector3 startLocalPosition;
        public bool useCurrentPositionAsEnd = true;
        public Vector3 endLocalPosition;

        [Header("Scale Settings")]
        public bool doScale = false;
        public Vector3 startScale = new Vector3(0.9f, 0.9f, 1f);
        public Vector3 endScale = new Vector3(1f, 1f, 1f);

        [Header("Ease Settings")]
        public Ease easeType = Ease.OutQuad;
        public float delay = 0f;
    }

    [Header("Animation Panels")]
    public List<PanelAnimation> panelAnimations;

    [Header("Auto-Advance Settings")]
    [Tooltip("E키를 누르지 않았을 때 다음 컷으로 넘어가는 대기 시간 (초)")]
    public float autoAdvanceDelay = 2.0f;

    [Header("Skip Settings")]
    public CanvasGroup skipPromptImage;
    public float skipHoldDuration = 3.0f;
    public float skipFadeInDuration = 1.0f;

    [Header("Cutscene End Settings")]
    public CanvasGroup fadeOutPanel;
    public float fadeOutDuration = 1.5f;

    [Header("Events")]
    [Tooltip("페이드 아웃이 완료된 후 실행할 이벤트")]
    public UnityEvent onCutsceneFinished;

    // --- Private Variables ---
    private IA_Player playerInput;
    private AudioSource audioSource;
    private Sequence currentPanelSequence;
    private int currentPanelIndex = 0;
    private bool isPanelPlaying = false;
    private float skipHoldTimer = 0f;
    private bool cutsceneEnded = false;

    private bool isSkippedToEnd = false; // 3. 스킵 완료 상태 플래그
    private float autoAdvanceTimer = 0f; // 4. 자동 진행 타이머

    // --- Unity Lifecycle ---
    void Awake()
    {
        playerInput = new IA_Player();
        audioSource = GetComponent<AudioSource>();

        // 모든 패널 초기 비활성화
        foreach (var anim in panelAnimations)
        {
            if (anim.targetPanel != null)
            {
                anim.targetPanel.SetActive(false);
            }
        }

        // UI 초기화
        if (skipPromptImage != null) skipPromptImage.alpha = 0f;
        if (fadeOutPanel != null)
        {
            fadeOutPanel.alpha = 0f;
            fadeOutPanel.gameObject.SetActive(true);
        }
    }

    private void OnEnable()
    {
        playerInput.Player.Enable();
    }

    private void OnDisable()
    {
        playerInput.Player.Disable();
    }

    private void OnDestroy()
    {
        playerInput.Dispose();
        currentPanelSequence?.Kill();
    }

    void Start()
    {
        currentPanelIndex = 0;
        isPanelPlaying = false;
        cutsceneEnded = false;
        isSkippedToEnd = false;
        autoAdvanceTimer = 0f;
    }

    // --- Input & Logic Update ---
    void Update()
    {
        // 3. 스킵 완료 후, E키 탭을 기다리는 상태
        if (isSkippedToEnd && !cutsceneEnded)
        {
            // 'Interact' (E) 키를 탭하면 OnCutsceneEnd() 실행
            if (playerInput.Player.Interact.WasPressedThisFrame())
            {
                OnCutsceneEnd(); // 페이드 아웃 및 이벤트 실행
            }
            return; // 다른 로직 실행 방지
        }

        // 컷신이 종료되었거나, 현재 패널이 애니메이션 중이면 타이머 리셋 후 중단
        if (cutsceneEnded || isPanelPlaying)
        {
            autoAdvanceTimer = 0f;
            return;
        }

        // 4. 'E' 키 탭 (즉시 진행)
        if (playerInput.Player.Interact.WasPressedThisFrame())
        {
            autoAdvanceTimer = 0f;  // 자동 진행 타이머 리셋
            skipHoldTimer = 0f;     // 스킵 타이머 리셋
            PlayNextPanel();        // 즉시 다음 패널 재생
            return; // 이번 프레임 처리 완료
        }

        // 'E' 키 홀드 (스킵 진행)
        if (playerInput.Player.Interact.IsPressed())
        {
            skipHoldTimer += Time.deltaTime;
            autoAdvanceTimer = 0f; // 홀드 중에는 자동 진행 안 함

            if (skipPromptImage != null)
            {
                skipPromptImage.alpha = Mathf.Clamp01(skipHoldTimer / skipFadeInDuration);
            }

            if (skipHoldTimer >= skipHoldDuration)
            {
                SkipCutscene(); // 스킵 함수 실행
            }
        }
        // 4. 'E' 키 안 누름 (자동 진행)
        else
        {
            // 스킵 UI/타이머 초기화
            skipHoldTimer = 0f;
            if (skipPromptImage != null) skipPromptImage.alpha = 0f;

            // 첫 번째 컷(index 0)은 E키로 시작해야 하므로, 
            // currentPanelIndex > 0 일 때만 자동 진행 타이머 작동
            if (currentPanelIndex > 0 && currentPanelIndex < panelAnimations.Count)
            {
                autoAdvanceTimer += Time.deltaTime;
                if (autoAdvanceTimer >= autoAdvanceDelay)
                {
                    autoAdvanceTimer = 0f;
                    PlayNextPanel(); // 2초(설정값) 후 다음 패널 자동 재생
                }
            }
        }
    }

    // --- Cutscene Methods ---

    public void PlayNextPanel()
    {
        autoAdvanceTimer = 0f; // 패널 재생 시 항상 타이머 리셋

        if (isPanelPlaying || cutsceneEnded) return;

        // 모든 패널 재생이 끝난 후 E키를 누르면 OnCutsceneEnd 호출
        if (currentPanelIndex >= panelAnimations.Count)
        {
            OnCutsceneEnd();
            return;
        }

        isPanelPlaying = true;
        PanelAnimation anim = panelAnimations[currentPanelIndex];

        currentPanelSequence?.Kill();
        currentPanelSequence = DOTween.Sequence();

        if (anim.targetPanel == null)
        {
            isPanelPlaying = false;
            currentPanelIndex++;
            PlayNextPanel(); // 대상 패널이 없으면 건너뛰기
            return;
        }

        Transform panelTransform = anim.targetPanel.transform;
        SpriteRenderer spriteRenderer = anim.targetPanel.GetComponent<SpriteRenderer>();

        Vector3 finalEndPosition = anim.useCurrentPositionAsEnd ?
            panelTransform.localPosition : anim.endLocalPosition;

        // 1. 패널 활성화 및 사운드 재생
        currentPanelSequence.AppendCallback(() =>
        {
            anim.targetPanel.SetActive(true);
            if (anim.panelSound != null)
            {
                audioSource.PlayOneShot(anim.panelSound, anim.panelSoundVolume);
            }
        });

        // 2. 초기 상태 설정
        Color startColor = Color.white;
        if (anim.doFade && spriteRenderer != null)
        {
            startColor = spriteRenderer.color;
            startColor.a = anim.startAlpha;
            spriteRenderer.color = startColor;
        }
        if (anim.doMove) panelTransform.localPosition = anim.startLocalPosition;
        if (anim.doScale) panelTransform.localScale = anim.startScale;

        // 3. 애니메이션 트윈 생성
        if (anim.doFade && spriteRenderer != null)
        {
            currentPanelSequence.Append(spriteRenderer
                .DOFade(anim.endAlpha, anim.duration)
                .SetEase(anim.easeType)
                .SetDelay(anim.delay));
        }
        if (anim.doMove)
        {
            currentPanelSequence.Join(panelTransform
                .DOLocalMove(finalEndPosition, anim.duration)
                .SetEase(anim.easeType)
                .SetDelay(anim.delay));
        }
        if (anim.doScale)
        {
            currentPanelSequence.Join(panelTransform
                .DOScale(anim.endScale, anim.duration)
                .SetEase(anim.easeType)
                .SetDelay(anim.delay));
        }

        // 4. 애니메이션 완료 시 콜백
        currentPanelSequence.OnComplete(() =>
        {
            isPanelPlaying = false; // "재생 중" 플래그 해제

            // 2. 마지막 패널(인덱스가 Count와 같아짐) 재생이 끝났다면
            //    즉시 OnCutsceneEnd() (페이드 아웃) 호출
            if (currentPanelIndex == panelAnimations.Count)
            {
                OnCutsceneEnd();
            }
        });

        currentPanelSequence.Play();
        currentPanelIndex++; // 다음 패널 인덱스로 이동
    }

    // 3. E키 꾹 눌러 스킵하는 함수
    private void SkipCutscene()
    {
        if (isSkippedToEnd || cutsceneEnded) return; // 중복 실행 방지

        isPanelPlaying = false;
        currentPanelSequence?.Kill();

        Debug.Log("컷신 스킵! 모든 패널을 최종 상태로 활성화합니다.");

        // 스킵 UI 초기화
        skipHoldTimer = 0f;
        if (skipPromptImage != null) skipPromptImage.alpha = 0f;

        // 모든 패널을 즉시 활성화하고 최종 상태로 설정
        foreach (var anim in panelAnimations)
        {
            if (anim.targetPanel == null) continue;

            anim.targetPanel.transform.DOKill();
            anim.targetPanel.GetComponent<SpriteRenderer>()?.DOKill();

            anim.targetPanel.SetActive(true); // 활성화

            // 최종 알파값 적용
            if (anim.doFade && anim.targetPanel.GetComponent<SpriteRenderer>() != null)
            {
                SpriteRenderer sr = anim.targetPanel.GetComponent<SpriteRenderer>();
                Color c = sr.color;
                c.a = anim.endAlpha;
                sr.color = c;
            }
            // 최종 크기 적용
            if (anim.doScale)
            {
                anim.targetPanel.transform.localScale = anim.endScale;
            }
            // 최종 위치 적용
            if (anim.doMove)
            {
                if (!anim.useCurrentPositionAsEnd)
                {
                    anim.targetPanel.transform.localPosition = anim.endLocalPosition;
                }
            }
        }

        // 3. 스킵 완료 플래그 설정
        isSkippedToEnd = true;
        currentPanelIndex = panelAnimations.Count; // 인덱스를 끝으로 보냄

        Debug.Log("모든 패널 활성화 완료. 다음 E키 입력을 대기합니다...");
    }

    // 1. 컷신 종료 (페이드 아웃 및 이벤트 실행) 함수
    private void OnCutsceneEnd()
    {
        if (cutsceneEnded) return; // 중복 실행 방지
        cutsceneEnded = true;

        // 타이머 및 재생 플래그 정리
        autoAdvanceTimer = 0f;
        isPanelPlaying = false;
        currentPanelSequence?.Kill();

        Debug.Log("컷신 종료... 페이드 아웃 시작");

        // 1. EndSound 로직 제거됨

        // 페이드 아웃 패널이 설정되어 있다면 페이드 아웃 실행
        if (fadeOutPanel != null)
        {
            fadeOutPanel.DOFade(1f, fadeOutDuration)
                .SetEase(Ease.Linear)
                .OnComplete(() => {
                    Debug.Log("페이드 아웃 완료. 이벤트 실행.");
                    onCutsceneFinished.Invoke(); // 페이드 아웃 후 이벤트 실행
                });
        }
        else
        {
            // 페이드 아웃 패널이 없으면 즉시 이벤트 실행
            Debug.Log("페이드 아웃 패널 없음. 이벤트 즉시 실행.");
            onCutsceneFinished.Invoke();
        }
    }
}