using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
/// 플레이어 관련 FEEL 호출 래퍼
/// - 널 안전 호출
/// - 월드 위치 지정 재생 오버로드
/// - 전체 정지 유틸 제공
/// </summary>
public class PlayerFeedbacks : MonoBehaviour
{
    [Header("FEEL Players")]
    [SerializeField] private MMF_Player _footstep;
    [SerializeField] private MMF_Player _land;
    [SerializeField] private MMF_Player _jump;

    /// <summary>발자국 FEEL</summary>
    public void Footstep() => _footstep?.PlayFeedbacks();

    /// <summary>착지 FEEL</summary>
    public void Land() => _land?.PlayFeedbacks();

    /// <summary>점프 FEEL</summary>
    public void Jump() => _jump?.PlayFeedbacks();

    [Header("Damage / Events")]
    [Tooltip("폭발 넉백 시 재생될 피드백 (스크린 셰이크, 비네트 등)")]
    [SerializeField] private MMF_Player _explosionHitFeedback;

    /// <summary>월드 좌표에서 재생하고 싶을 때(사운드/카메라 셰이크 등 위치 반영)</summary>
    public void Footstep(Vector3 worldPosition) => _footstep?.PlayFeedbacks(worldPosition);
    public void Land(Vector3 worldPosition) => _land?.PlayFeedbacks(worldPosition);
    public void Jump(Vector3 worldPosition) => _jump?.PlayFeedbacks(worldPosition);

    public void ExplosionHit() => _explosionHitFeedback?.PlayFeedbacks();
    /// <summary>모든 FEEL 즉시 정지</summary>
    public void StopAll()
    {
        _footstep?.StopFeedbacks();
        _land?.StopFeedbacks();
        _jump?.StopFeedbacks();
    }
}
