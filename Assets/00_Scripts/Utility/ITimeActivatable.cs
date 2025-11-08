using UnityEngine;

/// <summary>
/// TimeGun으로 쏠 수 있는 모든 '특수 상호작용' 오브젝트
/// (AI, 기계, 폭발물 등)가 구현해야 하는 인터페이스입니다.
/// </summary>
public interface ITimeActivatable
{
    /// <summary>
    /// TimeGun에 의해 호출되어 오브젝트의 시간 상태를 토글합니다.
    /// (t=0 -> t=1 또는 t=1 -> t=0)
    /// </summary>
    void ToggleTimeState();
}