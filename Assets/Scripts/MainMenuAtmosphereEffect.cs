using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuAtmosphereEffect : MonoBehaviour
{
    [Header("Warning Pulse")]
    [SerializeField] private bool enableWarningPulse = true;
    [SerializeField] private Color warningColor = new Color(1f, 0f, 0f, 0.05f);
    [SerializeField] private float pulseSpeed = 1.0f;
    [SerializeField, Range(0f, 0.15f)] private float pulseMaxAlpha = 0.035f;

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

    private void Awake()
    {
        CreateWarningPulseImage();
        CreateGlitchLines();
    }

    private void OnEnable()
    {
        if (glitchCoroutine != null)
            StopCoroutine(glitchCoroutine);

        glitchCoroutine = StartCoroutine(GlitchRoutine());
    }

    private void OnDisable()
    {
        if (glitchCoroutine != null)
            StopCoroutine(glitchCoroutine);
    }

    private void Update()
    {
        UpdateWarningPulse();
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

    private void UpdateWarningPulse()
    {
        if (warningPulseImage == null)
            return;

        if (!enableWarningPulse)
        {
            warningPulseImage.color = Color.clear;
            return;
        }

        float alpha = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f * pulseMaxAlpha;

        Color c = warningColor;
        c.a = alpha;
        warningPulseImage.color = c;
    }

    private IEnumerator GlitchRoutine()
    {
        while (true)
        {
            float wait = Random.Range(glitchMinInterval, glitchMaxInterval);
            yield return new WaitForSecondsRealtime(wait);

            if (!enableGlitch || glitchLines == null)
                continue;

            ShowGlitchLines();
            yield return new WaitForSecondsRealtime(glitchDuration);
            HideGlitchLines();
        }
    }

    private void ShowGlitchLines()
    {
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
        for (int i = 0; i < glitchLines.Length; i++)
        {
            if (glitchLines[i] != null)
                glitchLines[i].color = Color.clear;
        }
    }
}