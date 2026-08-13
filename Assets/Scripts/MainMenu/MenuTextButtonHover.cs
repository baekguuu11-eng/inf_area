using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuTextButtonHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Image underlineImage;

    [Header("Sound")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.85f;
    [SerializeField] private bool playHoverSound = true;
    [SerializeField] private bool playClickSound = true;

    [Header("Text Colors")]
    [SerializeField] private Color normalColor = new Color(0.70f, 0.95f, 1.0f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.95f, 1.0f, 1.0f, 1f);
    [SerializeField] private Color clickColor = Color.white;

    [Header("Text Scale")]
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1f);
    [SerializeField] private Vector3 clickScale = new Vector3(0.97f, 0.97f, 1f);

    [Header("Hover Pulse")]
    [SerializeField] private bool enableHoverPulse = true;
    [SerializeField] private float hoverPulseSpeed = 4f;
    [SerializeField, Range(0f, 0.25f)] private float hoverPulseStrength = 0.08f;

    [Header("Underline")]
    [SerializeField] private bool useUnderline = true;
    [SerializeField] private Color underlineColor = new Color(0.45f, 0.95f, 1.0f, 1f);
    [SerializeField] private float underlineHeight = 3f;

    [Header("Auto Underline Width")]
    [SerializeField] private bool useAutoUnderlineWidth = true;
    [SerializeField] private float startUnderlineWidth = 200f;
    [SerializeField] private float settingsUnderlineWidth = 275f;
    [SerializeField] private float makersUnderlineWidth = 240f;
    [SerializeField] private float quitUnderlineWidth = 155f;

    [Header("Manual Underline Width")]
    [SerializeField] private float manualUnderlineFullWidth = 200f;

    [Header("Animation")]
    [SerializeField] private float hoverAnimationDuration = 0.14f;
    [SerializeField] private float clickAnimationDuration = 0.08f;

    private RectTransform textRect;
    private RectTransform underlineRect;
    private bool isHovering;
    private bool isPressing;
    private bool hoverEnabled = true;
    private Coroutine animationCoroutine;

    private static AudioSource globalUISoundSource;
    private static float globalSfxVolume = 1f;

    public static void SetGlobalSfxVolume(float volume)
    {
        globalSfxVolume = Mathf.Clamp01(volume);
    }

    public static float GetGlobalSfxVolume()
    {
        return globalSfxVolume;
    }

    private void Reset()
    {
        labelText = GetComponentInChildren<TextMeshProUGUI>(true);

        Transform underline = FindUnderlineTransform();
        if (underline != null)
            underlineImage = underline.GetComponent<Image>();

        uiAudioSource = GetComponentInParent<AudioSource>();
    }

    private void Awake()
    {
        if (labelText == null)
            labelText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (underlineImage == null)
        {
            Transform underline = FindUnderlineTransform();
            if (underline != null)
                underlineImage = underline.GetComponent<Image>();
        }

        if (uiAudioSource == null)
            uiAudioSource = GetComponentInParent<AudioSource>();

        if (labelText != null)
        {
            textRect = labelText.GetComponent<RectTransform>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Overflow;

            if (textRect != null)
                textRect.pivot = new Vector2(0.5f, 0.5f);
        }

        if (underlineImage != null)
        {
            underlineRect = underlineImage.GetComponent<RectTransform>();
            underlineImage.color = underlineColor;
            underlineImage.gameObject.SetActive(true);
            SetUnderlineWidth(0f);
        }

        ApplyInstantNormalState();
    }

    private Transform FindUnderlineTransform()
    {
        Transform direct = transform.Find("Underline");
        if (direct != null)
            return direct;

        direct = transform.Find("UnderLine");
        if (direct != null)
            return direct;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Replace("_", "").ToLower().Contains("underline"))
                return child;
        }

        return null;
    }

    private void OnEnable()
    {
        isHovering = false;
        isPressing = false;
        ApplyInstantNormalState();
    }

    private void OnDisable()
    {
        isHovering = false;
        isPressing = false;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        ApplyInstantNormalState();
    }

    private void Update()
    {
        if (!hoverEnabled)
            return;

        if (!enableHoverPulse || !isHovering || isPressing || labelText == null)
            return;

        float pulse = (Mathf.Sin(Time.unscaledTime * hoverPulseSpeed) + 1f) * 0.5f;

        labelText.color = Color.Lerp(
            hoverColor,
            Color.white,
            pulse * hoverPulseStrength
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hoverEnabled)
            return;

        isHovering = true;
        PlayUISound(hoverSound, hoverVolume, playHoverSound);

        if (!isPressing)
            AnimateToHoverState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hoverEnabled)
            return;

        isHovering = false;
        isPressing = false;
        AnimateToNormalState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!hoverEnabled)
            return;

        isPressing = true;
        PlayUISound(clickSound, clickVolume, playClickSound);
        AnimateToClickState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!hoverEnabled)
            return;

        isPressing = false;

        if (isHovering)
            AnimateToHoverState();
        else
            AnimateToNormalState();
    }

    public void SetHoverEnabled(bool enabled)
    {
        hoverEnabled = enabled;

        if (!hoverEnabled)
        {
            isHovering = false;
            isPressing = false;
            AnimateToNormalState();
        }
    }

    private void PlayUISound(AudioClip clip, float volume, bool canPlay)
    {
        if (!canPlay || clip == null)
            return;

        AudioSource source = GetGlobalUISoundSource();
        if (source == null)
            return;

        source.PlayOneShot(
            clip,
            Mathf.Clamp01(volume) * globalSfxVolume
        );
    }

    private void AnimateToNormalState()
    {
        StartAnimation(normalColor, normalScale, 0f, hoverAnimationDuration);
    }

    private void AnimateToHoverState()
    {
        StartAnimation(
            hoverColor,
            hoverScale,
            useUnderline ? GetTargetUnderlineWidth() : 0f,
            hoverAnimationDuration
        );
    }

    private void AnimateToClickState()
    {
        StartAnimation(
            clickColor,
            clickScale,
            useUnderline ? GetTargetUnderlineWidth() : 0f,
            clickAnimationDuration
        );
    }

    private void StartAnimation(
        Color targetColor,
        Vector3 targetScale,
        float targetUnderlineWidth,
        float duration)
    {
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(
            AnimateRoutine(
                targetColor,
                targetScale,
                targetUnderlineWidth,
                duration
            )
        );
    }

    private IEnumerator AnimateRoutine(
        Color targetColor,
        Vector3 targetScale,
        float targetUnderlineWidth,
        float duration)
    {
        if (labelText == null)
            yield break;

        Color startColor = labelText.color;
        Vector3 startScale = textRect != null ? textRect.localScale : Vector3.one;
        float startUnderlineWidth = underlineRect != null ? underlineRect.sizeDelta.x : 0f;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            labelText.color = Color.Lerp(startColor, targetColor, smoothT);

            if (textRect != null)
                textRect.localScale = Vector3.Lerp(startScale, targetScale, smoothT);

            if (underlineRect != null)
            {
                SetUnderlineWidth(
                    Mathf.Lerp(
                        startUnderlineWidth,
                        targetUnderlineWidth,
                        smoothT
                    )
                );
            }

            yield return null;
        }

        labelText.color = targetColor;

        if (textRect != null)
            textRect.localScale = targetScale;

        if (underlineRect != null)
            SetUnderlineWidth(targetUnderlineWidth);

        animationCoroutine = null;
    }

    private void ApplyInstantNormalState()
    {
        if (labelText != null)
            labelText.color = normalColor;

        if (textRect != null)
            textRect.localScale = normalScale;

        SetUnderlineWidth(0f);
    }

    private float GetTargetUnderlineWidth()
    {
        if (!useAutoUnderlineWidth || labelText == null)
            return manualUnderlineFullWidth;

        string menuText = labelText.text.Trim().ToUpper();

        switch (menuText)
        {
            case "START":
                return startUnderlineWidth;

            case "SETTING":
            case "SETTINGS":
                return settingsUnderlineWidth;

            case "MAKERS":
                return makersUnderlineWidth;

            case "QUIT":
                return quitUnderlineWidth;

            default:
                return manualUnderlineFullWidth;
        }
    }

    private void SetUnderlineWidth(float width)
    {
        if (underlineImage == null || underlineRect == null)
            return;

        underlineImage.gameObject.SetActive(true);
        underlineImage.color = underlineColor;

        float finalWidth = Mathf.Max(0f, width);

        underlineRect.sizeDelta = new Vector2(
            finalWidth,
            underlineHeight
        );

        Color color = underlineImage.color;
        color.a = finalWidth <= 0.01f ? 0f : underlineColor.a;
        underlineImage.color = color;
    }

    private AudioSource GetGlobalUISoundSource()
    {
        if (globalUISoundSource != null)
            return globalUISoundSource;

        GameObject soundObject = new GameObject("Global_UI_Sound_Player");
        DontDestroyOnLoad(soundObject);

        globalUISoundSource = soundObject.AddComponent<AudioSource>();
        globalUISoundSource.playOnAwake = false;
        globalUISoundSource.loop = false;
        globalUISoundSource.spatialBlend = 0f;
        globalUISoundSource.volume = 1f;

        return globalUISoundSource;
    }
}
