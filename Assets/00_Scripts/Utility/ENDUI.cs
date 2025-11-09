using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(TextMeshProUGUI))]
public class EndingText : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeInDuration = 2f;
    public float glitchDuration = 3f;
    public float finalSwitchDelay = 1f;

    [Header("Glitch Settings")]
    public float noiseIntensity = 0.1f; // Èçµé¸² °­µµ
    public float flickerSpeed = 25f;    // ¹ÝÀü ±ôºýÀÓ ¼Óµµ
    public float colorInvertChance = 0.2f; // ¹ÝÀü È®·ü

    private TextMeshProUGUI endText;
    private string happyEndText = "HAPPY END";
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
        //1. ÆäÀÌµåÀÎ
        yield return StartCoroutine(FadeInText());

        // 2. ±Û¸®Ä¡ È¿°ú
        yield return StartCoroutine(GlitchEffect());

        //3. ¸¶Áö¸· 1ÃÊ µ¿¾È "ENDLESS"·Î ¹Ù²Ù°í »ç¶óÁü
        endText.text = endlessText;
        yield return StartCoroutine(FadeOutText());
        SceneManager.LoadScene("Title");
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

            // Èçµé¸² (Perlin noise)
            float offsetX = (Mathf.PerlinNoise(Time.time * 10, 0f) - 0.5f) * noiseIntensity * Screen.width * 0.01f;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 10) - 0.5f) * noiseIntensity * Screen.height * 0.01f;
            endText.rectTransform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);

            // ±ôºýÀÓ (·£´ý ¹ÝÀü)
            if (Random.value < colorInvertChance * Time.deltaTime * flickerSpeed)
            {
                endText.color = Color.white - endText.color; // »ö ¹ÝÀü
            }
            else
            {
                endText.color = baseColor;
            }

            // ÇÈ¼¿ ±úÁü (UV Distortion Èä³»)
            textMaterial.SetFloat("_FaceDilate", Mathf.Sin(Time.time * 50f) * 0.1f);

            yield return null;
        }

        //ÃÊ±âÈ­
        endText.rectTransform.localPosition = originalPos;
        endText.color = baseColor;
        textMaterial.SetFloat("_FaceDilate", 0);
    }
}
