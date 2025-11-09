using UnityEngine;
using UnityEngine.SceneManagement; // 1. 씬 관리를 위해 필수
using Sirenix.OdinInspector;

/// <summary>
/// 이 트리거(Collider)에 'Player' 태그를 가진 오브젝트가 닿으면
/// 지정된 씬을 로드합니다.
/// </summary>
[RequireComponent(typeof(Collider))] // 이 스크립트는 콜라이더가 필요함
public class SceneTrigger : MonoBehaviour
{
    [BoxGroup("Settings")]
    [Tooltip("로드할 씬의 이름 (Build Settings에 포함되어야 함)")]
    [SerializeField] private string sceneToLoad;

    [BoxGroup("Settings")]
    [Tooltip("트리거를 발동시킬 오브젝트의 태그 (기본값: Player)")]
    [SerializeField] private string triggerTag = "Player";

    private bool isLoading = false; // 중복 로드 방지 플래그

    private void Awake()
    {
        // 1. 콜라이더가 트리거로 설정되어 있는지 확인
        // (트리거가 아니면 OnCollisionEnter를 사용해야 함)
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning("SceneTrigger가 " + gameObject.name + "에 있지만, 콜라이더의 'Is Trigger'가 체크되지 않았습니다. OnCollisionEnter를 사용하려면 코드를 수정해야 합니다.");
        }
    }

    /// <summary>
    /// 콜라이더가 'Is Trigger'로 설정되어 있을 때 호출됩니다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 2. 이미 로딩 중이거나, 닿은 오브젝트의 태그가 'Player'가 아니면 무시
        if (isLoading || !other.CompareTag(triggerTag))
        {
            return;
        }

        // 3. 씬 로드 실행
        LoadTargetScene();
    }

    /// <summary>
    /// (선택 사항) 만약 'Is Trigger'를 체크하지 않고
    /// '물리적 충돌'로 씬을 로드하고 싶다면 이 함수를 사용하세요.
    /// </summary>
    // private void OnCollisionEnter(Collision collision)
    // {
    //     if (isLoading || !collision.gameObject.CompareTag(triggerTag))
    //     {
    //         return;
    //     }
    //     LoadTargetScene();
    // }

    private void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError("SceneTrigger: 로드할 씬 이름이 지정되지 않았습니다!", this.gameObject);
            return;
        }

        isLoading = true; // 중복 실행 방지
        Debug.Log($"씬 로드 시작: {sceneToLoad}");

        // 4. 씬을 비동기식이 아닌, 즉시 로드합니다.
        SceneManager.LoadScene(sceneToLoad);
    }
}