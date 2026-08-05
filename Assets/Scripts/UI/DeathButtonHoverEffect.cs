using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DeathButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Visual")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.15f, 0.15f, 0.18f);
    [SerializeField] private Color pressedColor = new Color(1f, 0.05f, 0.05f, 0.28f);

    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float pressedScale = 0.98f;
    [SerializeField] private float animationDuration = 0.08f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private float soundVolume = 0.5f;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Coroutine animationCoroutine;
    private bool isHovering;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;

        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage != null)
        {
            targetImage.color = normalColor;
            targetImage.raycastTarget = true;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        SetVisual(hoverColor, originalScale * hoverScale);
        PlaySound(hoverSound);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        SetVisual(normalColor, originalScale);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetVisual(pressedColor, originalScale * pressedScale);
        PlaySound(clickSound);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isHovering)
        {
            SetVisual(hoverColor, originalScale * hoverScale);
        }
        else
        {
            SetVisual(normalColor, originalScale);
        }
    }

    private void SetVisual(Color color, Vector3 targetScale)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale)
    {
        Vector3 startScale = rectTransform.localScale;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / animationDuration);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        rectTransform.localScale = targetScale;
        animationCoroutine = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null)
        {
            return;
        }

        if (clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, soundVolume);
    }
}