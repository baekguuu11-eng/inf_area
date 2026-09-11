using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CutsceneManager : MonoBehaviour
{
    [System.Serializable]
    public class CutsceneSlide
    {
        public Sprite image;

        [TextArea(2, 5)]
        public string dialogue;

        [Header("개별 설정 (-1이면 기본값 사용)")]
        public float imageRevealDuration = -1f;
        public float textStartDelay = -1f;
        public float characterInterval = -1f;

        [Header("등장 전 효과음")]
        public AudioClip soundBeforeReveal;
        [Min(0f)] public float soundBeforeRevealVolume = 1f;
        public float delayAfterSoundBeforeReveal = 0.2f;
        public bool waitForSoundBeforeReveal = true;

        [Header("등장 전 효과음 증폭")]
        [Min(1f)] public float soundBeforeRevealGain = 2.5f;
        [Range(1, 5)] public int soundBeforeRevealLayerCount = 2;
        [Range(0f, 0.05f)] public float soundBeforeRevealLayerDelay = 0.01f;

        [Header("등장 전 효과음 BGM 덕킹")]
        public bool duckBgmWhileSoundBeforeReveal = true;
        [Range(0f, 1f)] public float bgmDuckMultiplier = 0.25f;
        public float bgmRecoverDurationAfterSound = 0.3f;
    }

    [Header("Cutscene Slides")]
    [SerializeField] private CutsceneSlide[] slides;

    [Header("UI References")]
    [SerializeField] private Image cutsceneImage;
    [SerializeField] private Image fadePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private CanvasGroup dialogueCanvasGroup;
    [SerializeField] private CanvasGroup nextIndicatorCanvasGroup;

    [Header("Scene")]
    [SerializeField] private string nextSceneName = "GameScene";

    [Header("Default Timing")]
    [SerializeField] private float defaultImageRevealDuration = 1.5f;
    [SerializeField] private float defaultTextStartDelay = 0.25f;
    [SerializeField] private float defaultCharacterInterval = 0.045f;
    [SerializeField] private float betweenSlideFadeOutDuration = 0.35f;
    [SerializeField] private float finalFadeOutDuration = 1.0f;

    [Header("Input")]
    [SerializeField] private bool allowSpaceKey = true;
    [SerializeField] private bool allowEscapeSkipAll = true;

    [Header("Audio")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioClip cutsceneBgm;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
    [SerializeField] private float bgmFadeInDuration = 1.0f;
    [SerializeField] private float bgmFadeOutDuration = 1.0f;

    [Header("Typing Sound")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioClip typingSound;
    [SerializeField, Range(0f, 1f)] private float typingSoundVolume = 0.5f;
    [SerializeField] private float typingSoundMinInterval = 0.035f;
    [SerializeField] private bool playTypingSoundOnSpace = false;
    [SerializeField] private bool randomizeTypingPitch = true;
    [SerializeField] private float minTypingPitch = 0.95f;
    [SerializeField] private float maxTypingPitch = 1.05f;

    [Header("Next Indicator")]
    [SerializeField] private float indicatorBlinkSpeed = 2.0f;
    [SerializeField] private float indicatorMinAlpha = 0.25f;
    [SerializeField] private float indicatorMaxAlpha = 1.0f;

    [Header("Replay Option")]
    [SerializeField] private bool markAsWatchedWhenFinished = false;
    [SerializeField] private bool skipIfAlreadyWatched = false;
    [SerializeField] private string watchedPlayerPrefsKey = "OpeningCutscene_Watched";

    private int currentSlideIndex = 0;

    private bool isRevealing = false;
    private bool isTyping = false;
    private bool waitingForInput = false;
    private bool isEnding = false;

    private bool skipRevealRequested = false;
    private bool skipTypingRequested = false;

    private Coroutine indicatorCoroutine;
    private float nextTypingSoundTime = 0f;

    private void Reset()
    {
        slides = new CutsceneSlide[5];

        slides[0] = new CutsceneSlide
        {
            dialogue = "모든 것이 연결된 거대 정보 도시 국가.\n이곳의 질서는 하나의 중앙 시스템에 의해 유지되고 있었다."
        };

        slides[1] = new CutsceneSlide
        {
            dialogue = "도시의 중심에는 초거대 관리 시스템,\n‘아크’의 코어가 존재했다."
        };

        slides[2] = new CutsceneSlide
        {
            dialogue = "그러나 정체불명의 감염이 아크를 침식했다.\n도시를 지키던 시스템은 통제 불능의 위협으로 변했다."
        };

        slides[3] = new CutsceneSlide
        {
            dialogue = "기존의 방어 프로그램은 모두 실패했다.\n마지막 희망은 봉인되어 있던 자율형 백신, 라파엘이었다."
        };

        slides[4] = new CutsceneSlide
        {
            dialogue = "네, 맞습니다.\n바로 당신입니다."
        };
    }

    private IEnumerator Start()
    {
        Initialize();

        if (skipIfAlreadyWatched && PlayerPrefs.GetInt(watchedPlayerPrefsKey, 0) == 1)
        {
            LoadNextScene();
            yield break;
        }

        yield return PlayCutsceneRoutine();
    }

    private void Update()
    {
        if (isEnding)
            return;

        if (allowEscapeSkipAll && Input.GetKeyDown(KeyCode.Escape) && !GameplayPauseSettingsV14.EscapeIsReservedForPause)
        {
            RequestSkipAll();
            return;
        }

        if (IsAdvanceInputDown())
        {
            HandleAdvanceInput();
        }
    }

    private void Initialize()
    {
        if (cutsceneImage != null)
        {
            cutsceneImage.preserveAspect = true;
            cutsceneImage.raycastTarget = false;
        }

        if (fadePanel != null)
        {
            fadePanel.raycastTarget = false;
            SetFadeAlpha(1f);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "";
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.enableWordWrapping = true;
        }

        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = 1f;
            dialogueCanvasGroup.interactable = false;
            dialogueCanvasGroup.blocksRaycasts = false;
        }

        HideNextIndicator();

        if (bgmAudioSource != null)
        {
            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.loop = true;
            bgmAudioSource.volume = 0f;

            if (cutsceneBgm != null)
                bgmAudioSource.clip = cutsceneBgm;
        }

        if (sfxAudioSource != null)
        {
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.loop = false;
            sfxAudioSource.pitch = 1f;
        }
    }

    private IEnumerator PlayCutsceneRoutine()
    {
        PlayBgm();

        for (currentSlideIndex = 0; currentSlideIndex < slides.Length; currentSlideIndex++)
        {
            CutsceneSlide slide = slides[currentSlideIndex];

            if (slide == null)
                continue;

            SetupSlide(slide);

            yield return PlaySoundBeforeReveal(slide);

            float revealDuration = GetSlideImageRevealDuration(slide);
            yield return FadeScreenFromBlack(revealDuration);

            float textDelay = GetSlideTextStartDelay(slide);

            if (textDelay > 0f)
                yield return WaitUnscaled(textDelay);

            float charInterval = GetSlideCharacterInterval(slide);
            yield return TypeDialogue(slide.dialogue, charInterval);

            yield return WaitForNextInput();

            if (currentSlideIndex < slides.Length - 1)
            {
                yield return FadeScreenToBlack(betweenSlideFadeOutDuration);
            }
        }

        yield return EndCutsceneRoutine();
    }

    private void SetupSlide(CutsceneSlide slide)
    {
        skipRevealRequested = false;
        skipTypingRequested = false;
        waitingForInput = false;
        isRevealing = false;
        isTyping = false;

        HideNextIndicator();

        if (cutsceneImage != null)
            cutsceneImage.sprite = slide.image;

        if (dialogueText != null)
            dialogueText.text = "";

        SetFadeAlpha(1f);
    }

    private IEnumerator PlaySoundBeforeReveal(CutsceneSlide slide)
    {
        if (slide == null)
            yield break;

        if (slide.soundBeforeReveal == null)
            yield break;

        if (sfxAudioSource == null)
            yield break;

        float originalBgmVolume = 0f;
        bool shouldDuckBgm = slide.duckBgmWhileSoundBeforeReveal && bgmAudioSource != null && bgmAudioSource.isPlaying;

        if (shouldDuckBgm)
        {
            originalBgmVolume = bgmAudioSource.volume;
            bgmAudioSource.volume = originalBgmVolume * slide.bgmDuckMultiplier;
        }

        int layerCount = Mathf.Clamp(slide.soundBeforeRevealLayerCount, 1, 5);
        float finalVolume = Mathf.Max(0f, slide.soundBeforeRevealVolume * slide.soundBeforeRevealGain);

        for (int i = 0; i < layerCount; i++)
        {
            sfxAudioSource.pitch = 1f + Random.Range(-0.025f, 0.025f);
            sfxAudioSource.PlayOneShot(slide.soundBeforeReveal, finalVolume);

            if (slide.soundBeforeRevealLayerDelay > 0f && i < layerCount - 1)
                yield return new WaitForSecondsRealtime(slide.soundBeforeRevealLayerDelay);
        }

        // 핵심 수정:
        // 효과음 전체 길이를 기다리지 않음.
        // 지정한 짧은 시간만 기다린 뒤 바로 이미지가 나오게 함.
        float revealDelay = Mathf.Max(0f, slide.delayAfterSoundBeforeReveal);

        if (revealDelay > 0f)
            yield return new WaitForSecondsRealtime(revealDelay);

        // BGM 복구는 기다리지 않고 뒤에서 따로 실행.
        if (shouldDuckBgm)
        {
            StartCoroutine(RestoreBgmVolumeAfterDelay(
                originalBgmVolume,
                slide.soundBeforeReveal.length,
                slide.bgmRecoverDurationAfterSound
            ));
        }

        sfxAudioSource.pitch = 1f;
    }

    private IEnumerator RestoreBgmVolume(float targetVolume, float duration)
    {
        if (bgmAudioSource == null)
            yield break;

        float startVolume = bgmAudioSource.volume;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            bgmAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);

            yield return null;
        }

        bgmAudioSource.volume = targetVolume;
    }
    private IEnumerator RestoreBgmVolumeAfterDelay(float targetVolume, float delay, float recoverDuration)
    {
        float waitTime = Mathf.Max(0f, delay);

        if (waitTime > 0f)
            yield return new WaitForSecondsRealtime(waitTime);

        yield return RestoreBgmVolume(targetVolume, recoverDuration);
    }

    private IEnumerator FadeScreenFromBlack(float duration)
    {
        isRevealing = true;
        skipRevealRequested = false;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            if (skipRevealRequested)
                break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            SetFadeAlpha(Mathf.Lerp(1f, 0f, t));

            yield return null;
        }

        SetFadeAlpha(0f);

        isRevealing = false;
        skipRevealRequested = false;
    }

    private IEnumerator FadeScreenToBlack(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        float startAlpha = GetFadeAlpha();

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            SetFadeAlpha(Mathf.Lerp(startAlpha, 1f, t));

            yield return null;
        }

        SetFadeAlpha(1f);
    }

    private IEnumerator TypeDialogue(string fullText, float characterInterval)
    {
        isTyping = true;
        skipTypingRequested = false;

        if (dialogueText == null)
        {
            isTyping = false;
            yield break;
        }

        if (string.IsNullOrEmpty(fullText))
        {
            dialogueText.text = "";
            isTyping = false;
            yield break;
        }

        dialogueText.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipTypingRequested)
                break;

            dialogueText.text = fullText.Substring(0, i + 1);

            TryPlayTypingSound(fullText[i]);

            float elapsed = 0f;
            float safeInterval = Mathf.Max(0f, characterInterval);

            while (elapsed < safeInterval)
            {
                if (skipTypingRequested)
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        dialogueText.text = fullText;

        isTyping = false;
        skipTypingRequested = false;
    }

    private IEnumerator WaitForNextInput()
    {
        waitingForInput = true;
        ShowNextIndicator();

        while (waitingForInput && !isEnding)
        {
            yield return null;
        }

        waitingForInput = false;
        HideNextIndicator();
    }

    private void HandleAdvanceInput()
    {
        if (isRevealing)
        {
            skipRevealRequested = true;
            return;
        }

        if (isTyping)
        {
            skipTypingRequested = true;
            return;
        }

        if (waitingForInput)
        {
            waitingForInput = false;
        }
    }

    private bool IsAdvanceInputDown()
    {
        if (Input.GetMouseButtonDown(0))
            return true;

        if (allowSpaceKey && Input.GetKeyDown(KeyCode.Space))
            return true;

        return false;
    }

    private void RequestSkipAll()
    {
        if (isEnding)
            return;

        StopAllCoroutines();
        StartCoroutine(EndCutsceneRoutine());
    }

    private IEnumerator EndCutsceneRoutine()
    {
        if (isEnding)
            yield break;

        isEnding = true;
        HideNextIndicator();

        Coroutine bgmFadeOutCoroutine = null;

        if (bgmAudioSource != null)
            bgmFadeOutCoroutine = StartCoroutine(FadeAudio(bgmAudioSource, bgmAudioSource.volume, 0f, bgmFadeOutDuration));

        yield return FadeScreenToBlack(finalFadeOutDuration);

        if (bgmFadeOutCoroutine != null)
            yield return bgmFadeOutCoroutine;

        if (markAsWatchedWhenFinished)
        {
            PlayerPrefs.SetInt(watchedPlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("[CutsceneManager] Next Scene Name이 비어 있습니다.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void PlayBgm()
    {
        if (bgmAudioSource == null)
            return;

        if (bgmAudioSource.clip == null && cutsceneBgm != null)
            bgmAudioSource.clip = cutsceneBgm;

        if (bgmAudioSource.clip == null)
            return;

        bgmAudioSource.volume = 0f;
        bgmAudioSource.Play();

        StartCoroutine(FadeAudio(bgmAudioSource, 0f, bgmVolume, bgmFadeInDuration));
    }

    private IEnumerator FadeAudio(AudioSource audioSource, float startVolume, float targetVolume, float duration)
    {
        if (audioSource == null)
            yield break;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);

            yield return null;
        }

        audioSource.volume = targetVolume;

        if (Mathf.Approximately(targetVolume, 0f))
            audioSource.Stop();
    }

    private void TryPlayTypingSound(char typedChar)
    {
        if (sfxAudioSource == null || typingSound == null)
            return;

        if (!playTypingSoundOnSpace && char.IsWhiteSpace(typedChar))
            return;

        if (Time.unscaledTime < nextTypingSoundTime)
            return;

        nextTypingSoundTime = Time.unscaledTime + typingSoundMinInterval;

        if (randomizeTypingPitch)
            sfxAudioSource.pitch = Random.Range(minTypingPitch, maxTypingPitch);
        else
            sfxAudioSource.pitch = 1f;

        sfxAudioSource.PlayOneShot(typingSound, typingSoundVolume);
    }

    private void ShowNextIndicator()
    {
        if (nextIndicatorCanvasGroup == null)
            return;

        nextIndicatorCanvasGroup.gameObject.SetActive(true);

        if (indicatorCoroutine != null)
            StopCoroutine(indicatorCoroutine);

        indicatorCoroutine = StartCoroutine(NextIndicatorBlinkRoutine());
    }

    private void HideNextIndicator()
    {
        if (indicatorCoroutine != null)
        {
            StopCoroutine(indicatorCoroutine);
            indicatorCoroutine = null;
        }

        if (nextIndicatorCanvasGroup != null)
        {
            nextIndicatorCanvasGroup.alpha = 0f;
            nextIndicatorCanvasGroup.gameObject.SetActive(false);
        }
    }

    private IEnumerator NextIndicatorBlinkRoutine()
    {
        while (waitingForInput && nextIndicatorCanvasGroup != null)
        {
            float pingPong = Mathf.PingPong(Time.unscaledTime * indicatorBlinkSpeed, 1f);
            nextIndicatorCanvasGroup.alpha = Mathf.Lerp(indicatorMinAlpha, indicatorMaxAlpha, pingPong);

            yield return null;
        }
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadePanel == null)
            return;

        Color color = fadePanel.color;
        color.a = Mathf.Clamp01(alpha);
        fadePanel.color = color;
    }

    private float GetFadeAlpha()
    {
        if (fadePanel == null)
            return 0f;

        return fadePanel.color.a;
    }

    private float GetSlideImageRevealDuration(CutsceneSlide slide)
    {
        if (slide.imageRevealDuration >= 0f)
            return slide.imageRevealDuration;

        return defaultImageRevealDuration;
    }

    private float GetSlideTextStartDelay(CutsceneSlide slide)
    {
        if (slide.textStartDelay >= 0f)
            return slide.textStartDelay;

        return defaultTextStartDelay;
    }

    private float GetSlideCharacterInterval(CutsceneSlide slide)
    {
        if (slide.characterInterval >= 0f)
            return slide.characterInterval;

        return defaultCharacterInterval;
    }

    private void OnValidate()
    {
        defaultImageRevealDuration = Mathf.Max(0f, defaultImageRevealDuration);
        defaultTextStartDelay = Mathf.Max(0f, defaultTextStartDelay);
        defaultCharacterInterval = Mathf.Max(0f, defaultCharacterInterval);
        betweenSlideFadeOutDuration = Mathf.Max(0f, betweenSlideFadeOutDuration);
        finalFadeOutDuration = Mathf.Max(0f, finalFadeOutDuration);

        bgmFadeInDuration = Mathf.Max(0f, bgmFadeInDuration);
        bgmFadeOutDuration = Mathf.Max(0f, bgmFadeOutDuration);
        typingSoundMinInterval = Mathf.Max(0f, typingSoundMinInterval);

        minTypingPitch = Mathf.Max(0.01f, minTypingPitch);
        maxTypingPitch = Mathf.Max(minTypingPitch, maxTypingPitch);

        if (slides == null)
            return;

        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] == null)
                continue;

            slides[i].soundBeforeRevealVolume = Mathf.Max(0f, slides[i].soundBeforeRevealVolume);
            slides[i].soundBeforeRevealGain = Mathf.Max(1f, slides[i].soundBeforeRevealGain);
            slides[i].soundBeforeRevealLayerCount = Mathf.Clamp(slides[i].soundBeforeRevealLayerCount, 1, 5);
            slides[i].soundBeforeRevealLayerDelay = Mathf.Clamp(slides[i].soundBeforeRevealLayerDelay, 0f, 0.05f);
            slides[i].delayAfterSoundBeforeReveal = Mathf.Max(0f, slides[i].delayAfterSoundBeforeReveal);
            slides[i].bgmDuckMultiplier = Mathf.Clamp01(slides[i].bgmDuckMultiplier);
            slides[i].bgmRecoverDurationAfterSound = Mathf.Max(0f, slides[i].bgmRecoverDurationAfterSound);
        }
    }
}