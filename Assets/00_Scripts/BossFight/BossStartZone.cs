using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BossStartZone : MonoBehaviour
{
    [Tooltip("이 존이 보스전 시작을 알릴 레이저 컨트롤러")]
    public BossLaserController bossLaserController;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bossLaserController.Activate();
            Debug.Log("플레이어가 보스 구역에 진입했습니다. 보스전 시작!");
            gameObject.SetActive(false);
        }
    }
}
