using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneIntroManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private TextMeshProUGUI syncText;
    [SerializeField] private CanvasGroup introCanvasGroup;

    [Header("Player Control Lock")]
    [SerializeField] private Behaviour[] componentsToDisableDuringIntro;
    [SerializeField] private bool forceEnableComponentsAfterIntro = true;

    [Header("Intro Text")]
    [SerializeField] private string headerText = "[RAFAEL CORE SYNC]";
    [SerializeField] private string userSyncStartText = "사용자 동기화 시작";
    [SerializeField] private string syncPrefixText = "동기화율 ";
    [SerializeField] private int[] syncPercentValuesBeforeFailure = { 0, 24, 57, 83, 97 };
    [SerializeField] private int finalSyncPercentValue = 100;

    [SerializeField] private string vaccineCheckText = "백신 식별 코드 확인 중...";
    [SerializeField] private string identificationFailureText = "식별 실패";
    [SerializeField] private string deletedRecordText = "[기록 말소됨]";
    [SerializeField] private string emergencyAuthorityText = "비상 권한 강제 활성화";
    [SerializeField] private string syncCompleteText = "동기화 완료";

    [SerializeField] private string movementModuleText = "운동 모듈 재가동";
    [SerializeField] private string combatModuleText = "전투 모듈 재가동";
    [SerializeField] private string areaEntryText = "감염 구역 진입 허가";
    [SerializeField] private string finalProtocolText = "비상 정화 프로토콜을 실행합니다.";

    [Header("Text Timing")]
    [SerializeField] private float textCharacterInterval = 0.012f;
    [SerializeField] private float normalMessageHoldDuration = 0.22f;
    [SerializeField] private float headerHoldDuration = 0.3f;
    [SerializeField] private float syncPercentHoldDuration = 0.16f;
    [SerializeField] private float vaccineCheckHoldDuration = 0.25f;
    [SerializeField] private float failedIdentificationHoldDuration = 0.03f;
    [SerializeField] private float deletedRecordHoldDuration = 0.18f;
    [SerializeField] private float finalRevealDelay = 0.08f;
    [SerializeField] private float finalRevealDuration = 0.7f;

    [Header("Identification Failure Timing")]
    [SerializeField, Range(0.1f, 0.95f)] private float identificationInterruptRatio = 0.55f;

    [Header("Text Colors")]
    [SerializeField] private Color normalTextColor = new Color(0.75f, 1f, 0.95f, 1f);
    [SerializeField] private Color failedTextColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color deletedRecordColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color glitchTextColor = new Color(1f, 0.05f, 0.05f, 1f);

    [Header("Glitch Effect")]
    [SerializeField] private int glitchFlickerCount = 4;
    [SerializeField] private float glitchFlickerInterval = 0.018f;
    [SerializeField] private float glitchTextShakeAmount = 18f;
    [SerializeField] private float glitchFadeAlphaKick = 0.25f;
    [SerializeField] private string glitchCharacters = "#%@$!<>[]{}01ERROR";

    [Header("Intro Audio")]
    [SerializeField] private AudioSource introAudioSource;
    [SerializeField] private AudioClip typingSound;
    [SerializeField] private AudioClip glitchSound;
    [SerializeField, Range(0f, 1f)] private float typingSoundVolume = 0.45f;
    [SerializeField] private float typingSoundMinInterval = 0.025f;
    [SerializeField] private bool randomizeTypingPitch = true;
    [SerializeField] private float minTypingPitch = 0.95f;
    [SerializeField] private float maxTypingPitch = 1.05f;
    [SerializeField] private float glitchSoundVolume = 1f;

    [Header("Gameplay BGM After Intro")]
    [SerializeField] private AudioSource gameplayBgmAudioSource;
    [SerializeField] private AudioClip gameplayBgmClip;
    [SerializeField, Range(0f, 1f)] private float gameplayBgmTargetVolume = 0.7f;
    [SerializeField] private float gameplayBgmFadeInDuration = 1.0f;
    [SerializeField] private bool startBgmWhenScreenReveals = true;

    [Header("Input")]
    [SerializeField] private bool allowClickAdvance = true;
    [SerializeField] private bool allowSpaceAdvance = true;
    [SerializeField] private bool allowEscapeSkipAll = true;

    [Header("End Option")]
    [SerializeField] private bool disableIntroCanvasAfterFinish = true;

    private bool introRunning = false;
    private bool isTyping = false;
    private bool isWaitingForNext = false;
    private bool isFinishing = false;

    private bool completeCurrentTextRequested = false;
    private bool advanceCurrentMessageRequested = false;
    private bool skipAllRequested = false;
    private bool gameplayBgmStarted = false;

    private bool[] originalComponentEnabledStates;

    private RectTransform syncTextRectTransform;
    private Vector2 syncTextOriginalPosition;

    private float nextTypingSoundTime = 0f;

    private IEnumerator Start()
    {
        Initialize();
        LockPlayerControls();

        yield return PlayIntroRoutine();
    }

    private void Update()
    {
        if (!introRunning || isFinishing)
            return;

        if (allowEscapeSkipAll && Input.GetKeyDown(KeyCode.Escape))
        {
            SkipIntroImmediately();
            return;
        }

        if (IsAdvanceInputDown())
        {
            if (isTyping)
            {
                completeCurrentTextRequested = true;
            }
            else if (isWaitingForNext)
            {
                advanceCurrentMessageRequested = true;
            }
        }
    }

    private void Initialize()
    {
        if (syncPercentValuesBeforeFailure == null || syncPercentValuesBeforeFailure.Length == 0)
            syncPercentValuesBeforeFailure = new int[] { 0, 24, 57, 83, 97 };

        if (syncText != null)
        {
            syncText.text = "";
            syncText.color = normalTextColor;
            syncText.alignment = TextAlignmentOptions.Center;
            syncText.enableWordWrapping = true;

            syncTextRectTransform = syncText.GetComponent<RectTransform>();

            if (syncTextRectTransform != null)
                syncTextOriginalPosition = syncTextRectTransform.anchoredPosition;
        }

        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);
            fadePanel.raycastTarget = true;
            SetFadeAlpha(1f);
        }

        if (introCanvasGroup != null)
        {
            introCanvasGroup.gameObject.SetActive(true);
            introCanvasGroup.alpha = 1f;
            introCanvasGroup.interactable = false;
            introCanvasGroup.blocksRaycasts = true;
        }

        if (introAudioSource != null)
        {
            introAudioSource.playOnAwake = false;
            introAudioSource.loop = false;
            introAudioSource.pitch = 1f;
        }

        if (gameplayBgmAudioSource != null)
        {
            gameplayBgmAudioSource.playOnAwake = false;
            gameplayBgmAudioSource.loop = true;
            gameplayBgmAudioSource.volume = 0f;

            if (gameplayBgmClip != null)
                gameplayBgmAudioSource.clip = gameplayBgmClip;
        }

        skipAllRequested = false;
        isFinishing = false;
        introRunning = true;
        gameplayBgmStarted = false;
    }

    private IEnumerator PlayIntroRoutine()
    {
        introRunning = true;

        yield return TypeAndHold(headerText, normalTextColor, headerHoldDuration);
        yield return TypeAndHold(userSyncStartText, normalTextColor, normalMessageHoldDuration);

        yield return PlaySyncPercentRoutine(syncPercentValuesBeforeFailure);

        yield return TypeAndHold(vaccineCheckText, normalTextColor, vaccineCheckHoldDuration);

        yield return TypeIdentificationFailureWithEarlyGlitch(identificationFailureText, failedTextColor);

        yield return ShowDeletedRecordFast(deletedRecordText, deletedRecordColor);

        yield return TypeAndHold(emergencyAuthorityText, normalTextColor, normalMessageHoldDuration);

        yield return PlaySyncPercentRoutine(new int[] { finalSyncPercentValue });

        yield return TypeAndHold(syncCompleteText, normalTextColor, normalMessageHoldDuration);

        yield return TypeAndHold(movementModuleText, normalTextColor, 0.16f);
        yield return TypeAndHold(combatModuleText, normalTextColor, 0.16f);
        yield return TypeAndHold(areaEntryText, normalTextColor, 0.2f);

        yield return TypeAndHold(finalProtocolText, normalTextColor, 0.3f);

        yield return FinishIntroRoutine(false);
    }

    private IEnumerator TypeAndHold(string text, Color color, float holdDuration)
    {
        if (skipAllRequested)
            yield break;

        yield return TypeText(text, color);

        if (skipAllRequested)
            yield break;

        yield return WaitMessageHold(holdDuration);
    }

    private IEnumerator PlaySyncPercentRoutine(int[] percentValues)
    {
        if (syncText == null || percentValues == null || percentValues.Length == 0)
            yield break;

        completeCurrentTextRequested = false;
        advanceCurrentMessageRequested = false;

        syncText.color = normalTextColor;

        if (syncTextRectTransform != null)
            syncTextRectTransform.anchoredPosition = syncTextOriginalPosition;

        // 핵심: "동기화율 "은 한 번만 타이핑
        yield return TypeText(syncPrefixText, normalTextColor);

        if (skipAllRequested)
            yield break;

        // 그 다음부터는 숫자만 교체
        for (int i = 0; i < percentValues.Length; i++)
        {
            if (skipAllRequested)
                yield break;

            int clampedPercent = Mathf.Clamp(percentValues[i], 0, 100);

            syncText.text = syncPrefixText + clampedPercent + "%";

            TryPlayTypingSound('%');

            yield return WaitMessageHold(syncPercentHoldDuration);
        }
    }

    private IEnumerator TypeText(string fullText, Color textColor)
    {
        isTyping = true;
        completeCurrentTextRequested = false;

        if (syncText == null)
        {
            isTyping = false;
            yield break;
        }

        if (string.IsNullOrEmpty(fullText))
        {
            syncText.text = "";
            isTyping = false;
            yield break;
        }

        syncText.text = "";
        syncText.color = textColor;

        float interval = Mathf.Max(0f, textCharacterInterval);

        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipAllRequested || completeCurrentTextRequested)
                break;

            syncText.text = fullText.Substring(0, i + 1);
            TryPlayTypingSound(fullText[i]);

            float elapsed = 0f;

            while (elapsed < interval)
            {
                if (skipAllRequested || completeCurrentTextRequested)
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        syncText.text = fullText;

        isTyping = false;
        completeCurrentTextRequested = false;
    }

    private IEnumerator TypeIdentificationFailureWithEarlyGlitch(string fullText, Color textColor)
    {
        isTyping = true;
        completeCurrentTextRequested = false;

        if (syncText == null)
        {
            isTyping = false;
            yield break;
        }

        if (string.IsNullOrEmpty(fullText))
        {
            syncText.text = "";
            isTyping = false;
            yield break;
        }

        syncText.text = "";
        syncText.color = textColor;

        int interruptIndex = Mathf.Clamp(
            Mathf.CeilToInt(fullText.Length * identificationInterruptRatio),
            1,
            fullText.Length - 1
        );

        float interval = Mathf.Max(0f, textCharacterInterval);

        for (int i = 0; i < interruptIndex; i++)
        {
            if (skipAllRequested || completeCurrentTextRequested)
                break;

            syncText.text = fullText.Substring(0, i + 1);
            TryPlayTypingSound(fullText[i]);

            float elapsed = 0f;

            while (elapsed < interval)
            {
                if (skipAllRequested || completeCurrentTextRequested)
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        isTyping = false;
        completeCurrentTextRequested = false;

        if (skipAllRequested)
            yield break;

        if (failedIdentificationHoldDuration > 0f)
            yield return WaitUnscaled(failedIdentificationHoldDuration);

        yield return PlayIdentificationFailureGlitch(fullText, textColor);
    }

    private IEnumerator ShowDeletedRecordFast(string text, Color textColor)
    {
        isTyping = false;
        isWaitingForNext = false;

        if (syncText == null)
            yield break;

        syncText.color = textColor;
        syncText.text = text;

        if (introAudioSource != null && glitchSound != null)
        {
            introAudioSource.pitch = 1.05f;
            introAudioSource.PlayOneShot(glitchSound, glitchSoundVolume * 0.8f);
        }

        float originalAlpha = GetFadeAlpha();

        SetFadeAlpha(Mathf.Clamp01(originalAlpha - 0.12f));
        yield return WaitUnscaled(0.035f);
        SetFadeAlpha(originalAlpha);

        yield return WaitMessageHold(deletedRecordHoldDuration);

        if (syncText != null)
            syncText.text = "";
    }

    private IEnumerator WaitMessageHold(float duration)
    {
        isWaitingForNext = true;
        advanceCurrentMessageRequested = false;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration)
        {
            if (skipAllRequested || advanceCurrentMessageRequested)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        isWaitingForNext = false;
        advanceCurrentMessageRequested = false;
    }

    private IEnumerator PlayIdentificationFailureGlitch(string originalText, Color originalColor)
    {
        if (introAudioSource != null && glitchSound != null)
        {
            introAudioSource.pitch = 1f;
            introAudioSource.PlayOneShot(glitchSound, glitchSoundVolume);
        }

        float originalFadeAlpha = GetFadeAlpha();

        for (int i = 0; i < glitchFlickerCount; i++)
        {
            if (skipAllRequested)
                yield break;

            if (syncText != null)
            {
                syncText.color = glitchTextColor;

                if (Random.value > 0.35f)
                    syncText.text = GetGlitchedText(originalText);
                else
                    syncText.text = originalText;
            }

            if (syncTextRectTransform != null)
            {
                Vector2 randomOffset = new Vector2(
                    Random.Range(-glitchTextShakeAmount, glitchTextShakeAmount),
                    Random.Range(-glitchTextShakeAmount, glitchTextShakeAmount)
                );

                syncTextRectTransform.anchoredPosition = syncTextOriginalPosition + randomOffset;
            }

            if (fadePanel != null)
            {
                float kickedAlpha = Mathf.Clamp01(originalFadeAlpha - Random.Range(0.03f, glitchFadeAlphaKick));
                SetFadeAlpha(kickedAlpha);
            }

            yield return WaitUnscaled(glitchFlickerInterval);

            if (skipAllRequested)
                yield break;

            if (syncText != null)
            {
                syncText.color = originalColor;
                syncText.text = originalText;
            }

            if (syncTextRectTransform != null)
                syncTextRectTransform.anchoredPosition = syncTextOriginalPosition;

            SetFadeAlpha(originalFadeAlpha);

            yield return WaitUnscaled(glitchFlickerInterval);
        }

        if (syncText != null)
        {
            syncText.color = originalColor;
            syncText.text = originalText;
        }

        if (syncTextRectTransform != null)
            syncTextRectTransform.anchoredPosition = syncTextOriginalPosition;

        SetFadeAlpha(originalFadeAlpha);
    }

    private IEnumerator FinishIntroRoutine(bool instant)
    {
        if (isFinishing)
            yield break;

        isFinishing = true;
        introRunning = false;
        isTyping = false;
        isWaitingForNext = false;

        if (syncText != null)
            syncText.text = "";

        if (syncTextRectTransform != null)
            syncTextRectTransform.anchoredPosition = syncTextOriginalPosition;

        if (instant)
        {
            SetFadeAlpha(0f);
            StartGameplayBgm();
            UnlockPlayerControls();
            HideIntroCanvasIfNeeded();
            yield break;
        }

        if (finalRevealDelay > 0f)
            yield return WaitUnscaled(finalRevealDelay);

        // 화면이 전환되면서 BGM 시작
        if (startBgmWhenScreenReveals)
            StartGameplayBgm();

        yield return RevealGameScreen();

        if (!startBgmWhenScreenReveals)
            StartGameplayBgm();

        UnlockPlayerControls();
        HideIntroCanvasIfNeeded();
    }

    private IEnumerator RevealGameScreen()
    {
        if (fadePanel == null)
            yield break;

        float startAlpha = GetFadeAlpha();
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, finalRevealDuration);

        while (elapsed < duration)
        {
            if (skipAllRequested)
            {
                SetFadeAlpha(0f);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            SetFadeAlpha(Mathf.Lerp(startAlpha, 0f, t));

            yield return null;
        }

        SetFadeAlpha(0f);
    }

    private void StartGameplayBgm()
    {
        if (gameplayBgmStarted)
            return;

        if (gameplayBgmAudioSource == null)
            return;

        if (gameplayBgmAudioSource.clip == null && gameplayBgmClip != null)
            gameplayBgmAudioSource.clip = gameplayBgmClip;

        if (gameplayBgmAudioSource.clip == null)
            return;

        gameplayBgmStarted = true;

        gameplayBgmAudioSource.loop = true;
        gameplayBgmAudioSource.volume = 0f;
        gameplayBgmAudioSource.Play();

        StartCoroutine(FadeGameplayBgm());
    }

    private IEnumerator FadeGameplayBgm()
    {
        if (gameplayBgmAudioSource == null)
            yield break;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, gameplayBgmFadeInDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            gameplayBgmAudioSource.volume = Mathf.Lerp(0f, gameplayBgmTargetVolume, t);

            yield return null;
        }

        gameplayBgmAudioSource.volume = gameplayBgmTargetVolume;
    }

    private void SkipIntroImmediately()
    {
        if (isFinishing)
            return;

        skipAllRequested = true;
        StopAllCoroutines();
        StartCoroutine(FinishIntroRoutine(true));
    }

    private void LockPlayerControls()
    {
        if (componentsToDisableDuringIntro == null)
            return;

        originalComponentEnabledStates = new bool[componentsToDisableDuringIntro.Length];

        for (int i = 0; i < componentsToDisableDuringIntro.Length; i++)
        {
            Behaviour component = componentsToDisableDuringIntro[i];

            if (component == null)
                continue;

            originalComponentEnabledStates[i] = component.enabled;
            component.enabled = false;
        }
    }

    private void UnlockPlayerControls()
    {
        if (componentsToDisableDuringIntro == null)
            return;

        for (int i = 0; i < componentsToDisableDuringIntro.Length; i++)
        {
            Behaviour component = componentsToDisableDuringIntro[i];

            if (component == null)
                continue;

            if (forceEnableComponentsAfterIntro)
            {
                component.enabled = true;
            }
            else if (originalComponentEnabledStates != null && i < originalComponentEnabledStates.Length)
            {
                component.enabled = originalComponentEnabledStates[i];
            }
        }
    }

    private void HideIntroCanvasIfNeeded()
    {
        if (!disableIntroCanvasAfterFinish)
            return;

        if (introCanvasGroup != null)
        {
            introCanvasGroup.alpha = 0f;
            introCanvasGroup.blocksRaycasts = false;
            introCanvasGroup.interactable = false;
            introCanvasGroup.gameObject.SetActive(false);
        }
        else
        {
            if (fadePanel != null)
                fadePanel.raycastTarget = false;
        }
    }

    private void TryPlayTypingSound(char typedChar)
    {
        if (introAudioSource == null || typingSound == null)
            return;

        if (char.IsWhiteSpace(typedChar))
            return;

        if (Time.unscaledTime < nextTypingSoundTime)
            return;

        nextTypingSoundTime = Time.unscaledTime + Mathf.Max(0f, typingSoundMinInterval);

        if (randomizeTypingPitch)
            introAudioSource.pitch = Random.Range(minTypingPitch, maxTypingPitch);
        else
            introAudioSource.pitch = 1f;

        introAudioSource.PlayOneShot(typingSound, typingSoundVolume);
    }

    private bool IsAdvanceInputDown()
    {
        if (allowClickAdvance && Input.GetMouseButtonDown(0))
            return true;

        if (allowSpaceAdvance && Input.GetKeyDown(KeyCode.Space))
            return true;

        return false;
    }

    private string GetGlitchedText(string original)
    {
        if (string.IsNullOrEmpty(original))
            return "";

        if (string.IsNullOrEmpty(glitchCharacters))
            glitchCharacters = "#%@$!<>[]{}01ERROR";

        char[] result = original.ToCharArray();

        for (int i = 0; i < result.Length; i++)
        {
            if (char.IsWhiteSpace(result[i]))
                continue;

            if (Random.value < 0.6f)
            {
                int randomIndex = Random.Range(0, glitchCharacters.Length);
                result[i] = glitchCharacters[randomIndex];
            }
        }

        return new string(result);
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration)
        {
            if (skipAllRequested)
                yield break;

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

    private void OnValidate()
    {
        textCharacterInterval = Mathf.Max(0f, textCharacterInterval);
        normalMessageHoldDuration = Mathf.Max(0f, normalMessageHoldDuration);
        headerHoldDuration = Mathf.Max(0f, headerHoldDuration);
        syncPercentHoldDuration = Mathf.Max(0f, syncPercentHoldDuration);
        vaccineCheckHoldDuration = Mathf.Max(0f, vaccineCheckHoldDuration);
        failedIdentificationHoldDuration = Mathf.Max(0f, failedIdentificationHoldDuration);
        deletedRecordHoldDuration = Mathf.Max(0f, deletedRecordHoldDuration);
        finalRevealDelay = Mathf.Max(0f, finalRevealDelay);
        finalRevealDuration = Mathf.Max(0f, finalRevealDuration);

        identificationInterruptRatio = Mathf.Clamp(identificationInterruptRatio, 0.1f, 0.95f);

        glitchFlickerCount = Mathf.Max(0, glitchFlickerCount);
        glitchFlickerInterval = Mathf.Max(0f, glitchFlickerInterval);
        glitchTextShakeAmount = Mathf.Max(0f, glitchTextShakeAmount);
        glitchFadeAlphaKick = Mathf.Clamp01(glitchFadeAlphaKick);

        typingSoundMinInterval = Mathf.Max(0f, typingSoundMinInterval);
        minTypingPitch = Mathf.Max(0.01f, minTypingPitch);
        maxTypingPitch = Mathf.Max(minTypingPitch, maxTypingPitch);
        glitchSoundVolume = Mathf.Max(0f, glitchSoundVolume);

        gameplayBgmTargetVolume = Mathf.Clamp01(gameplayBgmTargetVolume);
        gameplayBgmFadeInDuration = Mathf.Max(0f, gameplayBgmFadeInDuration);

        finalSyncPercentValue = Mathf.Clamp(finalSyncPercentValue, 0, 100);

        if (syncPercentValuesBeforeFailure != null)
        {
            for (int i = 0; i < syncPercentValuesBeforeFailure.Length; i++)
                syncPercentValuesBeforeFailure[i] = Mathf.Clamp(syncPercentValuesBeforeFailure[i], 0, 100);
        }
    }
}