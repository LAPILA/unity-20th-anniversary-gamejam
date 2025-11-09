using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위해 필수

/// <summary>
/// 게임의 전반적인 상태 (엔딩 스택 등)를 관리하며,
/// DontDestroyOnLoad를 통해 씬이 바뀌어도 유지됩니다.
/// </summary>
public class GameEventManager : MonoBehaviour
{
    public static GameEventManager Instance { get; private set; }

    // [Header("엔딩 스택")]
    // [Tooltip("선택지에 따라 누적되는 엔딩 분기용 스택")]
    public int endingStack = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 이 오브젝트를 파괴하지 않음
        }
        else
        {
            // 이미 인스턴스가 존재하면 새로 생긴 것은 파괴
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 엔딩 스택을 추가하거나 감소시킵니다.
    /// </summary>
    /// <param name="amount">추가할 값 (음수도 가능)</param>
    public void AddEndingStack(int amount)
    {
        endingStack += amount;
        Debug.Log($"엔딩 스택 변경: {amount} (현재 총: {endingStack})");
    }

    /// <summary>
    /// 현재 엔딩 스택을 기준으로 실제 엔딩 씬을 로드합니다.
    /// </summary>
    public void LoadEndingScene()
    {
        // 씬 로드 전 플레이어 조작 등을 막는 로딩 UI를 띄우는 것이 좋습니다. (선택 사항)
        // 예: UIManager.Instance.ShowLoadingScreen();

        Debug.Log($"엔딩 씬 로드를 시도합니다. (현재 스택: {endingStack})");

        // 💥 이곳에서 스택 값에 따라 엔딩 씬 이름을 결정합니다.
        // 씬 이름은 File > Build Settings에 등록되어 있어야 합니다.
        if (endingStack >= 2)
        {
            SceneManager.LoadScene("Ending1"); // 엔딩 1 (회귀)
        }
        else
        {
            SceneManager.LoadScene("Ending2"); // 엔딩 2 (탈출)
        }
    }
}