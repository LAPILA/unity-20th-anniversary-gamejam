using UnityEngine;
using System.Collections.Generic;

public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public struct ParallaxLayer
    {
        public RectTransform transform;
        [Tooltip("움직임의 강도. 0은 움직이지 않음, 1은 마우스와 똑같이 움직임.")]
        [Range(0f, 1f)]
        public float moveFactor;
    }

    [SerializeField]
    private List<ParallaxLayer> layers;

    [SerializeField, Tooltip("마우스 움직임에 얼마나 민감하게 반응할지 결정합니다.")]
    private float sensitivity = 50f;

    private Vector2 _initialPosition;
    private Dictionary<RectTransform, Vector2> _initialLayerPositions;

    void Start()
    {
        // 각 레이어의 초기 위치를 저장
        _initialLayerPositions = new Dictionary<RectTransform, Vector2>();
        foreach (var layer in layers)
        {
            if (layer.transform != null)
            {
                _initialLayerPositions[layer.transform] = layer.transform.anchoredPosition;
            }
        }
    }

    void Update()
    {
        // 화면 중앙을 기준으로 마우스 위치 계산 (-0.5 ~ 0.5 범위)
        Vector2 mousePos = new Vector2(
            (Input.mousePosition.x / Screen.width) - 0.5f,
            (Input.mousePosition.y / Screen.height) - 0.5f
        );

        // 각 레이어를 moveFactor에 따라 이동
        foreach (var layer in layers)
        {
            if (layer.transform != null)
            {
                Vector2 targetPos = _initialLayerPositions[layer.transform] + (mousePos * sensitivity * layer.moveFactor);
                // 부드러운 움직임을 위해 Lerp 사용
                layer.transform.anchoredPosition = Vector2.Lerp(layer.transform.anchoredPosition, targetPos, Time.deltaTime * 5f);
            }
        }
    }
}
