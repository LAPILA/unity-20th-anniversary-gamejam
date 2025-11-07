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

    [BoxGroup("Settings")]
    [SerializeField] private float panelFadeDuration = 0.5f;

    [BoxGroup("Settings"), Required]
    [Tooltip("New Game 버튼 클릭 시 로드할 게임 씬의 이름")]
    [SerializeField] private string gameSceneName = "Test";

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 초기 상태 설정: 메인 메뉴만 활성화
        mainMenuPanel.alpha = 1f;
        mainMenuPanel.interactable = true;
    }


    // ---------- Public Button Callbacks ----------

    public void OnNewGameClicked()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("Game Scene Name이 비어있습니다! 인스펙터에서 설정해주세요.");
            return;
        }

        // SceneManager를 사용하여 씬을 로드합니다.
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- UI Helpers ----------

    private void SwitchPanel(CanvasGroup targetPanel)
    {
        // 모든 패널을 비활성화
        mainMenuPanel.interactable = false;
        mainMenuPanel.DOFade(0, panelFadeDuration);

        // 타겟 패널만 활성화
        targetPanel.interactable = true;
        targetPanel.DOFade(1, panelFadeDuration);
    }
}
