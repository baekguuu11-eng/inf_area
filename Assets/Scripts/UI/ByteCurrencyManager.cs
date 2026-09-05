using System;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ByteCurrencyManager : MonoBehaviour
{
    public static ByteCurrencyManager Instance { get; private set; }

    [Header("Currency")]
    [SerializeField] private int currentBytes = 0;
    [SerializeField] private string prefixText = "";

    [Header("UI")]
    [SerializeField] private TMP_Text byteText;
    [SerializeField] private RectTransform byteIcon;
    [SerializeField] private Canvas canvas;

    [Header("Camera")]
    [SerializeField] private Camera worldCamera;

    [Header("Collect Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip collectSound;
    [Range(0f, 1f)][SerializeField] private float collectVolume = 0.35f;
    [SerializeField] private float bounceScale = 1.22f;
    [SerializeField] private float bounceDuration = 0.16f;

    private Vector3 iconOriginalScale = Vector3.one;
    private Coroutine bounceCoroutine;
    private AudioClip generatedRustleClip;

    public int CurrentBytes { get { return currentBytes; } }
    public int CurrentByte { get { return currentBytes; } }
    public event Action<int> CurrencyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (worldCamera == null) worldCamera = Camera.main;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = collectVolume;
        if (byteIcon != null) iconOriginalScale = byteIcon.localScale;
        if (collectSound == null) generatedRustleClip = CreateSoftRustleClip();
        ConfigureByteText();
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (generatedRustleClip != null) Destroy(generatedRustleClip);
    }

    public void AddBytes(int amount)
    {
        if (amount <= 0) return;
        SetBytes(currentBytes + amount, true);
    }

    public void AddByte(int amount) { AddBytes(amount); }
    public bool CanSpend(int amount) { return amount <= 0 || currentBytes >= amount; }

    public bool SpendBytes(int amount)
    {
        if (amount <= 0) return true;
        if (!CanSpend(amount)) return false;
        SetBytes(currentBytes - amount, false);
        return true;
    }

    public bool SpendByte(int amount) { return SpendBytes(amount); }

    public void ResetBytes(int value = 0)
    {
        SetBytes(Mathf.Max(0, value), false);
    }

    private void SetBytes(int value, bool collectFeedback)
    {
        currentBytes = Mathf.Max(0, value);
        RefreshUI();
        CurrencyChanged?.Invoke(currentBytes);
        if (collectFeedback) PlayCollectFeedback();
    }

    public Vector3 GetByteIconWorldPosition()
    {
        if (byteIcon == null) return transform.position;
        if (canvas == null) canvas = byteIcon.GetComponentInParent<Canvas>();
        if (worldCamera == null) worldCamera = Camera.main;

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, byteIcon.position);
            if (worldCamera != null)
            {
                float distance = Mathf.Abs(worldCamera.transform.position.z);
                Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, distance));
                world.z = 0f;
                return world;
            }
        }

        return byteIcon.position;
    }

    private void RefreshUI()
    {
        if (byteText == null)
            return;

        ConfigureByteText();
        byteText.text = prefixText + currentBytes.ToString("N0");
    }

    private void ConfigureByteText()
    {
        if (byteText == null)
            return;

        byteText.textWrappingMode = TextWrappingModes.NoWrap;
        byteText.overflowMode = TextOverflowModes.Overflow;
        byteText.enableAutoSizing = false;
        byteText.raycastTarget = false;
        byteText.alignment = TextAlignmentOptions.Left;
        byteText.fontSize = 30f;

        TMP_FontAsset galmuri = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        if (galmuri != null)
            byteText.font = galmuri;

        RectTransform rect = byteText.rectTransform;
        if (rect == null)
            return;

        // Keep the value completely outside the Byte icon. The old 50 px centered text
        // rectangle overlapped the 90 px icon, which is why values such as 0/25 appeared
        // drawn on top of the token. Use a left-pivoted label with a fixed visual gap.
        rect.pivot = new Vector2(0f, 0.5f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50f);

        if (byteIcon != null && byteIcon.parent == rect.parent)
        {
            rect.anchorMin = byteIcon.anchorMin;
            rect.anchorMax = byteIcon.anchorMax;
            float iconWidth = byteIcon.rect.width * Mathf.Abs(byteIcon.localScale.x);
            float iconRight = byteIcon.anchoredPosition.x + iconWidth * (1f - byteIcon.pivot.x);
            rect.anchoredPosition = new Vector2(iconRight + 18f, byteIcon.anchoredPosition.y);
        }
    }

    private void PlayCollectFeedback()
    {
        if (audioSource != null)
        {
            AudioClip clip = collectSound != null ? collectSound : generatedRustleClip;
            if (clip != null) audioSource.PlayOneShot(clip, collectVolume);
        }

        if (byteIcon != null)
        {
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            bounceCoroutine = StartCoroutine(BounceIcon());
        }
    }

    private IEnumerator BounceIcon()
    {
        float half = Mathf.Max(0.02f, bounceDuration * 0.5f);
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            byteIcon.localScale = Vector3.Lerp(iconOriginalScale, iconOriginalScale * bounceScale, Mathf.Clamp01(elapsed / half));
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            byteIcon.localScale = Vector3.Lerp(iconOriginalScale * bounceScale, iconOriginalScale, Mathf.Clamp01(elapsed / half));
            yield return null;
        }
        byteIcon.localScale = iconOriginalScale;
        bounceCoroutine = null;
    }

    private AudioClip CreateSoftRustleClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.08f;
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float envelope = Mathf.Sin(t * Mathf.PI) * (1f - t);
            data[i] = UnityEngine.Random.Range(-1f, 1f) * envelope * 0.08f;
        }
        AudioClip clip = AudioClip.Create("ByteCollect_Runtime", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
