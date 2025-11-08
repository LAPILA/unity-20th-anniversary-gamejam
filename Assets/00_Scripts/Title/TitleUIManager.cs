using DG.Tweening;
using DG.Tweening.Core.Easing;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleUIManager : MonoBehaviour
{
    public static TitleUIManager Instance { get; private set; }

    [BoxGroup("Panels"), Required]
    [SerializeField] private CanvasGroup mainMenuPanel;

    // 1. 씬 전환 페이드아웃용 패널
    [BoxGroup("Panels"), Required]
    [Tooltip("씬 전환 시 화면을 어둡게 할 검은색 패널의 CanvasGroup")]
    [SerializeField] private CanvasGroup fadePanel;

    [BoxGroup("Feedbacks"), Required]
    [Tooltip("버튼 위에 마우스를 올렸을 때 재생될 MMF_Player")]
    [SerializeField] private MMF_Player buttonHoverFeedback;

    [BoxGroup("Feedbacks"), Required]
    [Tooltip("버튼을 클릭했을 때 재생될 MMF_Player")]
    [SerializeField] private MMF_Player buttonClickFeedback;

    [BoxGroup("Settings")]
    [SerializeField] private float panelFadeDuration = 0.5f;

    // 2. 씬 전환 페이드 시간
    [BoxGroup("Settings")]
    [Tooltip("씬 전환 시 어두워지는 데 걸리는 시간(초)")]
    [SerializeField] private float sceneFadeDuration = 4.0f;

    [BoxGroup("Settings"), Required]
    [Tooltip("New Game 버튼 클릭 시 로드할 게임 씬의 이름")]
    [SerializeField] private string gameSceneName = "Test";

    // 3. 페이드 중복 실행 방지 플래그
    private bool _isFading = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 메인 메뉴 초기 상태
        mainMenuPanel.alpha = 1f;
        mainMenuPanel.interactable = true;

        // 4. 페이드 패널 초기 상태 설정 (투명, 비활성)
        if (fadePanel != null)
        {
            fadePanel.alpha = 0f;
            fadePanel.interactable = false;
            fadePanel.blocksRaycasts = false; // 평소에는 입력 방해 X
        }
        else
        {
            Debug.LogError("Fade Panel이 인스펙터에 할당되지 않았습니다!");
        }
    }


    // ---------- Public Button Callbacks ----------

    /// <summary>
    /// 'New Game' 버튼 클릭 시 페이드 아웃 후 씬 전환
    /// </summary>
    public void OnNewGameClicked()
    {
        // 5. 페이드 아웃 로직

        // 중복 실행 방지
        if (_isFading) return;
        _isFading = true;

        buttonClickFeedback?.PlayFeedbacks(); // 클릭 피드백

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("Game Scene Name이 비어있습니다! 인스펙터에서 설정해주세요.");
            _isFading = false; // 플래그 리셋
            return;
        }

        // 페이드 패널 유효성 검사
        if (fadePanel == null)
        {
            Debug.LogError("Fade Panel이 할당되지 않아 즉시 씬을 로드합니다.");
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        // 페이드 동안 모든 UI 입력 차단
        fadePanel.blocksRaycasts = true;

        // 4초(sceneFadeDuration) 동안 서서히 화면을 어둡게 처리
        fadePanel.DOFade(1f, sceneFadeDuration)
            .SetEase(Ease.Linear) // 균일한 속도
            .OnComplete(() =>
            {
                // 페이드 완료 시 씬 로드
                SceneManager.LoadScene(gameSceneName);
            });
    }

    /// <summary>
    /// 'Quit' 버튼 클릭 시 페이드 아웃 후 종료
    /// </summary>
    public void OnQuitClicked()
    {
        // 중복 실행 방지
        if (_isFading) return;
        _isFading = true;

        buttonClickFeedback?.PlayFeedbacks(); // 클릭 피드백

        if (fadePanel == null)
        {
            QuitApplication(); // 페이드 패널 없으면 즉시 종료
            return;
        }

        // 페이드 동안 UI 입력 차단
        fadePanel.blocksRaycasts = true;

        // 종료는 1초간 페이드
        fadePanel.DOFade(1f, 1.0f)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                // 페이드 완료 시 애플리케이션 종료
                QuitApplication();
            });
    }

    /// <summary>
    /// 실제 게임 종료 로직 (에디터/빌드 분기)
    /// </summary>
    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- UI Helpers ----------

    /// <summary>
    /// UI 버튼의 EventTrigger(PointerEnter)에 연결 (호버 피드백)
    /// </summary>
    public void OnButtonHover()
    {
        // 페이드 중에는 호버 피드백 방지
        if (_isFading) return;
        buttonHoverFeedback?.PlayFeedbacks();
    }

    /// <summary>
    /// 패널 전환 (현재 미사용)
    /// </summary>
    private void SwitchPanel(CanvasGroup targetPanel)
    {
        // 모든 패널 비활성화
        mainMenuPanel.interactable = false;
        mainMenuPanel.DOFade(0, panelFadeDuration);

        // 타겟 패널만 활성화
        targetPanel.interactable = true;
        targetPanel.DOFade(1, panelFadeDuration);
    }
}