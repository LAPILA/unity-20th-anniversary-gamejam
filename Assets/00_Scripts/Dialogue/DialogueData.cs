using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine; // VCam 추가를 위해 필요

/// <summary>
/// 선택지 버튼 하나를 정의하는 구조체
/// </summary>
[System.Serializable]
public struct ChoiceOption
{
    // 선택지 버튼에 표시될 텍스트
    [TextArea(1, 3)]
    public string buttonText_Korean;
    [TextArea(1, 3)]
    public string buttonText_English;

    // 이 선택지를 선택했을 때 재생할 다음 대화 목록 (DialogueData)
    // 이 필드가 null이 아니면, 이 선택지가 새로운 대화 분기의 시작점이 됩니다.
    public DialogueData nextDialogue;


    [Header("엔딩 스택")]
    [Tooltip("이 선택지를 고를 시 추가할 엔딩 스택 (음수도 가능)")]
    public int endingStackToAdd;
}

/// <summary>
/// 개별 대사 한 줄을 정의하는 구조체
/// </summary>
[System.Serializable]
public struct DialogueLine
{
    // 1. 화자 정보
    public string speakerName;       // "플레이어", "동료A", "수상한 NPC"
    public Sprite portrait;          // 표시될 초상화
    public bool isPlayer;            // 플레이어 대사인가? (L/R 슬라이드 구분에 사용)

    // 2. 동적 이벤트 (선택 사항)
    [Header("Dynamic Events (Optional)")]
    [Tooltip("이 대사가 시작될 때 재생할 사운드 클립 (언더테일 스타일 대사 소리)")]
    public AudioClip dialogueSound;

    [Tooltip("이 대사가 나올 때 VCam을 다른 위치로 이동시킬 Cinemachine Virtual Camera")]
    public CinemachineCamera overrideVCam;

    [Tooltip("이 대사가 나올 때 화면을 흔들 정도 (0.0: 흔들림 없음)")]
    [Range(0f, 1f)]
    public float cameraShakeIntensity;

    [Header("Branching (Optional)")]
    [Tooltip("이 대사가 끝난 후 선택지를 표시할지 여부")]
    public bool isChoicePoint;

    [Tooltip("선택지 1 (isChoicePoint가 true일 때 사용)")]
    public ChoiceOption choice1;

    [Tooltip("선택지 2 (isChoicePoint가 true일 때 사용, 2개 선택지 제한)")]
    public ChoiceOption choice2;

    // 3. 다국어 텍스트
    [Header("Text")]
    [TextArea(3, 5)]
    public string text_Korean;
    [TextArea(3, 5)]
    public string text_English;
}

/// <summary>
/// 하나의 '대화(Conversation)'를 정의하는 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Observation/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("대화 큐")]
    public List<DialogueLine> lines;

    [Header("엔딩 설정")]
    [Tooltip("이 대화가 끝나면 엔딩 씬 로드를 시도합니다.")]
    public bool isEndingTrigger = false;
}