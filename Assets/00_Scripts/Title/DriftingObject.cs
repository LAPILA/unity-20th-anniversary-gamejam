using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 씬에서 오브젝트를 천천히 회전시키고 한 방향으로 이동시킵니다.
/// 타이틀 화면의 배경을 꾸미는 소행성, 우주 쓰레기, 성운 등에 사용하기 좋습니다.
/// 
/// [사용법]
/// 1. 씬에 3D 오브젝트(Cube, Sphere, 또는 커스텀 모델)를 배치합니다.
/// 2. 이 스크립트를 해당 오브젝트에 추가합니다.
/// 3. 인스펙터에서 'Rotation Speed'와 'Drift Speed'를 원하는 값으로 조절합니다.
/// </summary>
public class DriftingObject : SerializedMonoBehaviour
{
    [BoxGroup("Settings")]
    [Tooltip("초당 회전 속도 (축별로 설정)")]
    public Vector3 rotationSpeed = new Vector3(0f, 10.0f, 2.0f);

    [BoxGroup("Settings")]
    [Tooltip("월드 좌표 기준 초당 이동 속도 (방향과 속도)")]
    public Vector3 driftSpeed = new Vector3(0.1f, 0f, 0f);

    private Transform _transform;

    void Awake()
    {
        // 매 프레임 GetComponent() 호출을 피하기 위해 트랜스폼을 캐시합니다.
        _transform = transform;
    }

    void Update()
    {
        // 1. 오브젝트 자체 축(Space.Self)을 기준으로 회전시킵니다.
        _transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);

        // 2. 월드 축(Space.World)을 기준으로 천천히 이동시킵니다.
        _transform.Translate(driftSpeed * Time.deltaTime, Space.World);
    }
}