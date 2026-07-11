using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuAtmosphereEffect : MonoBehaviour
{
    [Header("Warning Pulse")]
    [SerializeField] private bool enableWarningPulse = true;
    [SerializeField] private Color warningColor = new Color(1f, 0f, 0f, 1f);

    [Tooltip("빨간 분위기가 기본으로 깔리는 정도")]
    [SerializeField, Range(0f, 0.08f)] private float baseRedAlpha = 0.012f;

    [Tooltip("빨간 경고등이 맥박처럼 추가로 강해지는 정도")]
    [SerializeField, Range(0f, 0.15f)] private float pulseMaxAlpha = 0.025f;

    [SerializeField] private float pulseSpeed = 0.8f;

    [Header("Atmosphere Fade In")]
    [Tooltip("버튼들이 다 올라온 뒤 빨간 분위기가 서서히 나타나는 시간")]
    [SerializeField] private float effectFadeInDuration = 1.8f;

    [Header("Glitch")]
    [SerializeField] private bool enableGlitch = true;
    [SerializeField] private float glitchMinInterval = 8f;
    [SerializeField] private float glitchMaxInterval = 14f;
    [SerializeField] private float glitchDuration = 0.05f;
    [SerializeField] private int glitchLineCount = 3;
    [SerializeField] private float glitchLineHeight = 3f;
    [SerializeField] private Color glitchRed = new Color(1f, 0f, 0f, 0.35f);
    [SerializeField] private Color glitchCyan = new Color(0f, 1f, 1f, 0.25f);

    private Image warningPulseImage;
    private Image[] glitchLines;

    private Coroutine glitchCoroutine;
    private Coroutine fadeCoroutine;

    private bool effectsEnabled = false;
    private float effectIntensity = 0f;

    private void Awake()
    {
        CreateWarningPulseImage();
        CreateGlitchLines();
        DisableEffects();
    }

    private void OnDisable()
    {
        StopAllEffectCoroutines();
    }

    private void Update()
    {
        UpdateWarningPulse();
    }

    public void EnableEffects()
    {
        effectsEnabled = true;
        effectIntensity = 0f;

        if (warningPulseImage != null)
            warningPulseImage.color = Color.clear;

        HideGlitchLines();

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeInAtmosphereRoutine());

        if (glitchCoroutine != null)
            StopCoroutine(glitchCoroutine);

        glitchCoroutine = StartCoroutine(GlitchRoutine());
    }

    public void DisableEffects()
    {
        effectsEnabled = false;
        effectIntensity = 0f;

        StopAllEffectCoroutines();

        if (warningPulseImage != null)
            warningPulseImage.color = Color.clear;

        HideGlitchLines();
    }

    private void StopAllEffectCoroutines()
    {
        if (glitchCoroutine != null)
        {
            StopCoroutine(glitchCoroutine);
            glitchCoroutine = null;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    private void CreateWarningPulseImage()
    {
        GameObject obj = new GameObject("Atmosphere_WarningPulse");
        obj.transform.SetParent(transform, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        warningPulseImage = obj.AddComponent<Image>();
        warningPulseImage.raycastTarget = false;
        warningPulseImage.color = Color.clear;
    }

    private void CreateGlitchLines()
    {
        glitchLineCount = Mathf.Clamp(glitchLineCount, 1, 10);
        glitchLines = new Image[glitchLineCount];

        for (int i = 0; i < glitchLineCount; i++)
        {
            GameObject obj = new GameObject("Atmosphere_GlitchLine_" + i);
            obj.transform.SetParent(transform, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, glitchLineHeight);

            Image img = obj.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = Color.clear;

            glitchLines[i] = img;
        }
    }

    private IEnumerator FadeInAtmosphereRoutine()
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, effectFadeInDuration);

        effectIntensity = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / safeDuration);

            // 부드러운 Ease In Out
            float smoothT = t * t * (3f - 2f * t);

            effectIntensity = smoothT;

            yield return null;
        }

        effectIntensity = 1f;
        fadeCoroutine = null;
    }

    private void UpdateWarningPulse()
    {
        if (warningPulseImage == null)
            return;

        if (!effectsEnabled || !enableWarningPulse)
        {
            warningPulseImage.color = Color.clear;
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float pulseAlpha = pulse * pulseMaxAlpha;

        float finalAlpha = (baseRedAlpha + pulseAlpha) * effectIntensity;

        Color c = warningColor;
        c.a = finalAlpha;

        warningPulseImage.color = c;
    }

    private IEnumerator GlitchRoutine()
    {
        while (effectsEnabled)
        {
            float wait = Random.Range(glitchMinInterval, glitchMaxInterval);
            yield return new WaitForSecondsRealtime(wait);

            if (!effectsEnabled || !enableGlitch)
                continue;

            ShowGlitchLines();
            yield return new WaitForSecondsRealtime(glitchDuration);
            HideGlitchLines();
        }
    }

    private void ShowGlitchLines()
    {
        if (glitchLines == null)
            return;

        for (int i = 0; i < glitchLines.Length; i++)
        {
            if (glitchLines[i] == null)
                continue;

            RectTransform rt = glitchLines[i].GetComponent<RectTransform>();

            float y = Random.Range(-350f, 350f);
            float x = Random.Range(-25f, 25f);
            float width = Random.Range(450f, 900f);

            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, glitchLineHeight);

            glitchLines[i].color = i % 2 == 0 ? glitchRed : glitchCyan;
        }
    }

    private void HideGlitchLines()
    {
        if (glitchLines == null)
            return;

        for (int i = 0; i < glitchLines.Length; i++)
        {
            if (glitchLines[i] != null)
                glitchLines[i].color = Color.clear;
        }
    }
}