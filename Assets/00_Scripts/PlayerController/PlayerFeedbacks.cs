using UnityEngine;
using MoreMountains.Feedbacks;
using System.Collections.Generic; // List/배열이 비어있는지 확인하기 위해 추가 (선택)

/// <summary>
/// 플레이어 관련 FEEL 호출 래퍼 (v2: 랜덤 재생, 사망 연출 추가)
/// - 널 안전 호출
/// - 월드 위치 지정 재생 오버로드
/// - 전체 정지 유틸 제공
/// </summary>
public class PlayerFeedbacks : MonoBehaviour
{
    [Header("FEEL Players (Movement)")]
    [Tooltip("발소리 피드백 배열. 이 중 하나가 랜덤으로 재생됩니다. (3개 권장)")]
    [SerializeField] private MMF_Player[] _footsteps;

    [Header("Damage / Events")]
    [Tooltip("폭발 넉백 시 재생될 피드백 (스크린 셰이크, 비네트 등)")]
    [SerializeField] private MMF_Player _explosionHitFeedback;

    [Tooltip("사망 시 재생될 피드백 (사운드 + VCam 전환 + PostProcess 등)")]
    [SerializeField] private MMF_Player _death; // 사망 연출 MMF_Player 추가

    // --- Movement Feedbacks ---

    /// <summary>발자국 FEEL (랜덤 재생)</summary>
    public void Footstep() => PlayRandomFeedback(_footsteps);


    // --- Movement Feedbacks (World Position) ---

    /// <summary>월드 좌표에서 발자국 FEEL 재생</summary>
    public void Footstep(Vector3 worldPosition) => PlayRandomFeedback(_footsteps, worldPosition);

    // --- Event Feedbacks ---

    /// <summary>폭발 피격 FEEL</summary>
    public void ExplosionHit() => _explosionHitFeedback?.PlayFeedbacks();

    /// <summary>사망 FEEL (VCam 전환, 사운드, 이펙트 등)</summary>
    public void Death() => _death?.PlayFeedbacks();

    // --- Utility ---

    /// <summary>모든 FEEL 즉시 정지</summary>
    public void StopAll()
    {
        StopFeedbacksInArray(_footsteps);
        _explosionHitFeedback?.StopFeedbacks();
        _death?.StopFeedbacks();
    }

    /// <summary>배열 내의 모든 MMF_Player를 중지시키는 헬퍼 함수</summary>
    private void StopFeedbacksInArray(MMF_Player[] players)
    {
        if (players == null) return;
        foreach (var player in players)
        {
            player?.StopFeedbacks();
        }
    }

    /// <summary>
    /// MMF_Player 배열에서 랜덤으로 하나를 골라 재생하는 헬퍼 함수
    /// </summary>
    /// <param name="players">MMF_Player 배열</param>
    /// <param name="position">재생할 월드 위치 (옵션)</param>
    private void PlayRandomFeedback(MMF_Player[] players, Vector3? position = null)
    {
        // 배열이 비어있으면 아무것도 하지 않음
        if (players == null || players.Length == 0) return;

        // 0부터 배열 길이-1 까지의 랜덤 인덱스 선택
        int index = Random.Range(0, players.Length);

        MMF_Player playerToPlay = players[index];
        if (playerToPlay == null) return; // 선택된 MMF_Player가 null이면 무시

        // 위치값(position)이 제공되었는지에 따라 다른 PlayFeedbacks 함수 호출
        if (position.HasValue)
        {
            playerToPlay.PlayFeedbacks(position.Value);
        }
        else
        {
            playerToPlay.PlayFeedbacks();
        }
    }
}