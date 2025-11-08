/// <summary>
/// 플레이어가 상호작용할 수 있는 모든 오브젝트(NPC, 문, 아이템)가
/// 구현해야 하는 인터페이스입니다.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 플레이어가 상호작용 키("Interact")를 눌렀을 때 호출될 함수
    /// </summary>
    /// <param name="interactor">상호작용을 시도한 플레이어</param>
    void Interact(PlayerInteractor interactor);

    /// <summary>
    /// 플레이어가 상호작용 범위에 들어왔을 때 호출될 함수 (예: 아웃라인 표시)
    /// </summary>
    void OnPlayerEnterRange();

    /// <summary>
    /// 플레이어가 상호작용 범위에서 나갔을 때 호출될 함수 (예: 아웃라인 숨기기)
    /// </summary>
    void OnPlayerExitRange();
}