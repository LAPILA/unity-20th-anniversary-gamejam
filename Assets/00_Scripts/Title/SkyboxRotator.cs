using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 씬의 스카이박스를 천천히 지속적으로 회전시킵니다.
/// 타이틀 화면이나 로비 등에서 사용하기 좋습니다.
/// 
/// [사용법]
/// 1. 씬에 빈 GameObject를 하나 생성합니다. (이름 예: _SkyManager)
/// 2. 이 스크립트를 해당 GameObject에 추가합니다.
/// 3. 인스펙터에서 'Rotation Speed'를 조절합니다. (0.5 ~ 2 추천)
/// </summary>
public class SkyboxRotator : SerializedMonoBehaviour
{
    [BoxGroup("Settings")]
    [Tooltip("스카이박스 회전 속도 (초당 각도)")]
    public float rotationSpeed = 1.0f;

    private Material _skyboxInstance;
    private float _currentRotation;
    private int _rotationPropID;

    void Start()
    {
        // 1. 현재 씬의 스카이박스 머티리얼을 기반으로 새 인스턴스(복사본)를 생성합니다.
        //    (원본 머티리얼 에셋이 변경되는 것을 방지합니다)
        _skyboxInstance = new Material(RenderSettings.skybox);

        // 2. 씬의 스카이박스를 이 복사본으로 교체합니다.
        RenderSettings.skybox = _skyboxInstance;

        // 3. 머티리얼의 "_Rotation" 프로퍼티 ID를 캐시합니다. (최적화)
        _rotationPropID = Shader.PropertyToID("_Rotation");

        // 4. 현재 회전 값을 읽어옵니다. (기본값 0)
        if (_skyboxInstance.HasProperty(_rotationPropID))
        {
            _currentRotation = _skyboxInstance.GetFloat(_rotationPropID);
        }
    }

    void Update()
    {
        // 1. 시간에 따라 회전 값을 천천히 누적시킵니다.
        // Time.deltaTime을 곱해 프레임 속도와 관계없이 일정한 속도를 보장합니다.
        _currentRotation += rotationSpeed * Time.deltaTime;

        // 2. 값이 무한정 커지는 것을 방지하기 위해 360도로 나머지 연산을 합니다.
        if (_currentRotation > 360f) _currentRotation -= 360f;
        if (_currentRotation < 0f) _currentRotation += 360f;

        // 3. 스카이박스 머티리얼에 최종 회전 값을 적용합니다.
        _skyboxInstance.SetFloat(_rotationPropID, _currentRotation);
    }

    void OnDestroy()
    {
        // 씬이 파괴될 때 복사본 머티리얼도 함께 파괴합니다.
        // (이걸 안 하면 에디터에서 메모리 누수가 발생할 수 있습니다)
        if (_skyboxInstance != null)
        {
            Destroy(_skyboxInstance);
        }
    }
}