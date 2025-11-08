using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 개별 대사 한 줄을 정의하는 구조체
/// </summary>
[System.Serializable]
public struct DialogueLine
{
    // 1. 화자 정보
    public string speakerName;     // "플레이어", "동료A", "수상한 NPC"
    public Sprite portrait;        // 표시될 초상화
    public bool isPlayer;          // 플레이어 대사인가? (L/R 슬라이드 구분에 사용)

    // 2. 다국어 텍스트
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
}