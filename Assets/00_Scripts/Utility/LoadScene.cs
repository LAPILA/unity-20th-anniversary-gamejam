using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour
{
    /// <summary>
    /// 인스펙터 또는 다른 스크립트에서 이 함수를 호출하여
    /// 이름이 일치하는 씬을 로드합니다.
    /// </summary>
    /// <param name="sceneName">로드할 씬의 이름 (빌드 설정 기준)</param>
    public void LoadtheScene(string sceneName)
    {
        // 씬 이름이 비어있지 않은지 확인
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("로드할 씬 이름이 지정되지 않았습니다!");
            return;
        }

        Debug.Log($"{sceneName} 씬을 로드합니다...");

        // 2. 지정된 이름의 씬을 로드
        SceneManager.LoadScene(sceneName);
    }
}