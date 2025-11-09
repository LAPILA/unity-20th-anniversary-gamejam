using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
public class BADENDUI : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeInDuration = 2f;
    public float glitchDuration = 3f;
    public float finalSwitchDelay = 1f;

    [Header("Glitch Settings")]
    public float noiseIntensity = 0.2f; // 흔들림 강도
    public float flickerSpeed = 25f;    // 반전 깜빡임 속도
    public float colorInvertChance = 0.2f; // 반전 확률

    private TextMeshProUGUI endText;
    private string happyEndText = "BAD END";
    private string endlessText = "ENDLESS";
    private Material textMaterial;
    private Color baseColor;

    private void Start()
    {
        endText = GetComponent<TextMeshProUGUI>();
        textMaterial = endText.fontMaterial;
        baseColor = endText.color;
        endText.text = happyEndText;
        StartCoroutine(PlayEndingSequence());
    }

    private IEnumerator PlayEndingSequence()
    {
        //1. 페이드인
        yield return StartCoroutine(FadeInText());

        // 2. 글리치 효과
        yield return StartCoroutine(GlitchEffect());

        //3. 마지막 1초 동안 "ENDLESS"로 바꾸고 사라짐
        endText.text = endlessText;
        yield return StartCoroutine(FadeOutText());
    }

    IEnumerator FadeInText()
    {
        Color c = endText.color;
        c.a = 0;
        endText.color = c;

        float t = 0;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0, 1, t / fadeInDuration);
            endText.color = c;
            yield return null;
        }
    }

    IEnumerator FadeOutText()
    {
        Color c = endText.color;
        float t = 0;
        while (t < finalSwitchDelay)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1, 0, t / finalSwitchDelay);
            endText.color = c;
            yield return null;
        }
    }

    IEnumerator GlitchEffect()
    {
        float timer = 0f;
        Vector3 originalPos = endText.rectTransform.localPosition;

        while (timer < glitchDuration)
        {
            timer += Time.deltaTime;

            // 흔들림 (Perlin noise)
            float offsetX = (Mathf.PerlinNoise(Time.time * 10, 0f) - 0.5f) * noiseIntensity * Screen.width * 0.01f;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 10) - 0.5f) * noiseIntensity * Screen.height * 0.01f;
            endText.rectTransform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);

            // 깜빡임 (랜덤 반전)
            if (Random.value < colorInvertChance * Time.deltaTime * flickerSpeed)
            {
                endText.color = Color.white - endText.color; // 색 반전
            }
            else
            {
                endText.color = baseColor;
            }

            // 픽셀 깨짐 (UV Distortion 흉내)
            textMaterial.SetFloat("_FaceDilate", Mathf.Sin(Time.time * 50f) * 0.1f);

            yield return null;
        }

        //초기화
        endText.rectTransform.localPosition = originalPos;
        endText.color = baseColor;
        textMaterial.SetFloat("_FaceDilate", 0);
    }
}
