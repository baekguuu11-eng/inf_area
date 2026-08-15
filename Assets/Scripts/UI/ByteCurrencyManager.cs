using System.Collections;
using TMPro;
using UnityEngine;

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

    public int CurrentBytes => currentBytes;

    // ShopManager 호환용
    public int CurrentByte => currentBytes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
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
        audioSource.volume = collectVolume;

        if (byteIcon != null)
        {
            iconOriginalScale = byteIcon.localScale;
        }

        if (collectSound == null)
        {
            generatedRustleClip = CreateSoftRustleClip();
        }

        RefreshUI();
    }

    public void AddBytes(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentBytes += amount;
        RefreshUI();
        PlayCollectFeedback();
    }

    // 기존 코드 호환용
    public void AddByte(int amount)
    {
        AddBytes(amount);
    }

    public bool CanSpend(int amount)
    {
        return currentBytes >= amount;
    }

    public bool SpendBytes(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (currentBytes < amount)
        {
            return false;
        }

        currentBytes -= amount;
        RefreshUI();
        return true;
    }

    // 다른 코드 호환용
    public bool SpendByte(int amount)
    {
        return SpendBytes(amount);
    }

    public Vector3 GetByteIconWorldPosition()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (byteIcon == null || worldCamera == null)
        {
            return Vector3.zero;
        }

        Vector3 screenPosition = RectTransformUtility.WorldToScreenPoint(null, byteIcon.position);
        float distanceFromCamera = Mathf.Abs(worldCamera.transform.position.z);

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera)
        );

        worldPosition.z = 0f;
        return worldPosition;
    }

    private void RefreshUI()
    {
        if (byteText == null)
        {
            return;
        }

        byteText.text = prefixText + currentBytes.ToString();
    }

    private void PlayCollectFeedback()
    {
        PlayCollectSound();
        PlayIconBounce();
    }

    private void PlayCollectSound()
    {
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = collectSound != null ? collectSound : generatedRustleClip;

        if (clip == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(clip, collectVolume);
    }

    private void PlayIconBounce()
    {
        if (byteIcon == null)
        {
            return;
        }

        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
        }

        bounceCoroutine = StartCoroutine(BounceIconRoutine());
    }

    private IEnumerator BounceIconRoutine()
    {
        float halfDuration = bounceDuration * 0.5f;
        float elapsed = 0f;

        Vector3 targetScale = iconOriginalScale * bounceScale;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            byteIcon.localScale = Vector3.Lerp(iconOriginalScale, targetScale, EaseOutBack(t));
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            byteIcon.localScale = Vector3.Lerp(targetScale, iconOriginalScale, EaseOutBack(t));
            yield return null;
        }

        byteIcon.localScale = iconOriginalScale;
        bounceCoroutine = null;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private AudioClip CreateSoftRustleClip()
    {
        int sampleRate = 44100;
        float length = 0.18f;
        int sampleCount = Mathf.RoundToInt(sampleRate * length);
        float[] samples = new float[sampleCount];

        System.Random random = new System.Random();
        float lowFrequency = 95f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float normalized = i / (float)(sampleCount - 1);

            float attack = Mathf.Clamp01(normalized / 0.12f);
            float release = Mathf.Clamp01((1f - normalized) / 0.55f);
            float envelope = attack * release;

            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float low = Mathf.Sin(2f * Mathf.PI * lowFrequency * time) * 0.18f;

            samples[i] = (noise * 0.16f + low) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("Generated_Byte_SoftRustle", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}