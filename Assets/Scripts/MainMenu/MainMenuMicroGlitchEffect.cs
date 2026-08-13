using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuMicroGlitchEffect : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float minInterval = 14f;
    [SerializeField] private float maxInterval = 28f;
    [SerializeField] private float minDuration = 0.018f;
    [SerializeField] private float maxDuration = 0.045f;

    [Header("Appearance")]
    [SerializeField] private int maxLinesPerGlitch = 2;
    [SerializeField] private float minLineHeight = 1f;
    [SerializeField] private float maxLineHeight = 3f;
    [SerializeField] private float minLineWidth = 260f;
    [SerializeField] private float maxLineWidth = 850f;
    [SerializeField] private Color cyanGlitch = new Color(0f, 1f, 1f, 0.09f);
    [SerializeField] private Color redGlitch = new Color(1f, 0.05f, 0.05f, 0.075f);

    private Image[] lines;
    private Coroutine glitchRoutine;

    private void Awake()
    {
        CreateLines();
        HideLines();
    }

    private void OnEnable()
    {
        if (glitchRoutine != null)
            StopCoroutine(glitchRoutine);

        glitchRoutine = StartCoroutine(GlitchRoutine());
    }

    private void OnDisable()
    {
        if (glitchRoutine != null)
        {
            StopCoroutine(glitchRoutine);
            glitchRoutine = null;
        }

        HideLines();
    }

    private void CreateLines()
    {
        int count = Mathf.Clamp(maxLinesPerGlitch, 1, 3);
        lines = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject obj = new GameObject(
                "MicroGlitchLine_" + i,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            obj.transform.SetParent(transform, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image image = obj.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.clear;

            lines[i] = image;
        }
    }

    private IEnumerator GlitchRoutine()
    {
        while (true)
        {
            float wait = Random.Range(
                Mathf.Max(1f, minInterval),
                Mathf.Max(minInterval, maxInterval)
            );

            yield return new WaitForSecondsRealtime(wait);

            int activeCount = Random.Range(
                1,
                Mathf.Clamp(maxLinesPerGlitch, 1, lines.Length) + 1
            );

            ShowLines(activeCount);

            float duration = Random.Range(
                Mathf.Max(0.005f, minDuration),
                Mathf.Max(minDuration, maxDuration)
            );

            yield return new WaitForSecondsRealtime(duration);

            HideLines();

            if (Random.value < 0.18f)
            {
                yield return new WaitForSecondsRealtime(0.025f);
                ShowLines(1);
                yield return new WaitForSecondsRealtime(0.018f);
                HideLines();
            }
        }
    }

    private void ShowLines(int count)
    {
        if (lines == null)
            return;

        for (int i = 0; i < lines.Length; i++)
        {
            Image image = lines[i];

            if (image == null)
                continue;

            if (i >= count)
            {
                image.color = Color.clear;
                continue;
            }

            RectTransform rt = image.rectTransform;

            rt.anchoredPosition = new Vector2(
                Random.Range(-35f, 35f),
                Random.Range(-390f, 390f)
            );

            rt.sizeDelta = new Vector2(
                Random.Range(minLineWidth, maxLineWidth),
                Random.Range(minLineHeight, maxLineHeight)
            );

            image.color = i % 2 == 0
                ? cyanGlitch
                : redGlitch;
        }
    }

    private void HideLines()
    {
        if (lines == null)
            return;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] != null)
                lines[i].color = Color.clear;
        }
    }

    private void OnValidate()
    {
        minInterval = Mathf.Max(1f, minInterval);
        maxInterval = Mathf.Max(minInterval, maxInterval);

        minDuration = Mathf.Max(0.005f, minDuration);
        maxDuration = Mathf.Max(minDuration, maxDuration);

        maxLinesPerGlitch = Mathf.Clamp(
            maxLinesPerGlitch,
            1,
            3
        );
    }
}
