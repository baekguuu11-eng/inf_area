using System.Collections;
using UnityEngine;

public class MainMenuIntroAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup menuGroupCanvasGroup;
    [SerializeField] private RectTransform backgroundRect;
    [SerializeField] private RectTransform[] menuItems;
    [SerializeField] private MainMenuManager mainMenuManager;

    [Header("Optional Effects")]
    [SerializeField] private MainMenuBackgroundParallax backgroundParallax;
    [SerializeField] private MainMenuAtmosphereEffect atmosphereEffect;

    [Header("Audio Spectrum")]
    [SerializeField] private MainMenuAudioSpectrum audioSpectrum;

    [Header("Background Intro")]
    [SerializeField] private bool playIntroOnStart = true;
    [SerializeField] private float introStartDelay = 0.1f;
    [SerializeField] private float backgroundStartOffsetY = -120f;
    [SerializeField] private float backgroundMoveDuration = 0.55f;

    [Header("Menu Intro")]
    [SerializeField] private float itemStartOffsetY = -18f;
    [SerializeField] private float itemFadeDuration = 0.16f;
    [SerializeField] private float itemDelayAfterComplete = 0.04f;

    [Header("Start Click Fade")]
    [SerializeField] private float menuFadeOutDuration = 0.2f;

    private Vector2 backgroundOriginalPosition;
    private Vector2[] itemOriginalPositions;
    private CanvasGroup[] itemCanvasGroups;
    private MenuTextButtonHover[] hoverScripts;

    private bool isStarting = false;

    private void Awake()
    {
        if (menuGroupCanvasGroup == null)
            menuGroupCanvasGroup = GetComponent<CanvasGroup>();

        if (menuGroupCanvasGroup == null)
            menuGroupCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheBackground();
        CacheItems();
        CacheAudioSpectrum();
        PrepareIntroState();
    }

    private void Start()
    {
        if (playIntroOnStart)
            StartCoroutine(PlayIntroRoutine());
        else
            ShowAllInstantly();
    }

    private void CacheBackground()
    {
        if (backgroundRect != null)
            backgroundOriginalPosition = backgroundRect.anchoredPosition;
    }

    private void CacheItems()
    {
        if (menuItems == null || menuItems.Length == 0)
        {
            menuItems = new RectTransform[transform.childCount];

            for (int i = 0; i < transform.childCount; i++)
                menuItems[i] = transform.GetChild(i).GetComponent<RectTransform>();
        }

        itemOriginalPositions = new Vector2[menuItems.Length];
        itemCanvasGroups = new CanvasGroup[menuItems.Length];
        hoverScripts = new MenuTextButtonHover[menuItems.Length];

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null)
                continue;

            itemOriginalPositions[i] = menuItems[i].anchoredPosition;

            CanvasGroup cg = menuItems[i].GetComponent<CanvasGroup>();
            if (cg == null)
                cg = menuItems[i].gameObject.AddComponent<CanvasGroup>();

            itemCanvasGroups[i] = cg;
            hoverScripts[i] = menuItems[i].GetComponent<MenuTextButtonHover>();
        }
    }

    private void CacheAudioSpectrum()
    {
        if (audioSpectrum == null)
            audioSpectrum = Object.FindAnyObjectByType<MainMenuAudioSpectrum>();
    }

    private void PrepareIntroState()
    {
        menuGroupCanvasGroup.alpha = 1f;
        menuGroupCanvasGroup.interactable = false;
        menuGroupCanvasGroup.blocksRaycasts = false;

        if (audioSpectrum != null)
            audioSpectrum.HideInstant();

        if (backgroundParallax != null)
            backgroundParallax.SetParallaxEnabled(false);

        if (atmosphereEffect != null)
            atmosphereEffect.DisableEffects();

        if (backgroundRect != null)
            backgroundRect.anchoredPosition = backgroundOriginalPosition + new Vector2(0f, backgroundStartOffsetY);

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null || itemCanvasGroups[i] == null)
                continue;

            itemCanvasGroups[i].alpha = 0f;
            menuItems[i].anchoredPosition = itemOriginalPositions[i] + new Vector2(0f, itemStartOffsetY);

            if (hoverScripts[i] != null)
                hoverScripts[i].SetHoverEnabled(false);
        }
    }

    private IEnumerator PlayIntroRoutine()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(introStartDelay);

        yield return StartCoroutine(AnimateBackgroundIn());

        for (int i = 0; i < menuItems.Length; i++)
        {
            yield return StartCoroutine(AnimateItemIn(i));

            if (itemDelayAfterComplete > 0f)
                yield return new WaitForSecondsRealtime(itemDelayAfterComplete);
        }

        if (audioSpectrum != null)
            audioSpectrum.PlayFadeIn();

        EnableMenuInteraction();
        EnableMenuEffects();
    }

    private IEnumerator AnimateBackgroundIn()
    {
        if (backgroundRect == null)
            yield break;

        Vector2 startPos = backgroundOriginalPosition + new Vector2(0f, backgroundStartOffsetY);
        Vector2 endPos = backgroundOriginalPosition;

        backgroundRect.anchoredPosition = startPos;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, backgroundMoveDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            backgroundRect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);

            yield return null;
        }

        backgroundRect.anchoredPosition = endPos;

        if (backgroundParallax != null)
            backgroundParallax.SetBasePosition(endPos);
    }

    private IEnumerator AnimateItemIn(int index)
    {
        if (index < 0 || index >= menuItems.Length)
            yield break;

        RectTransform item = menuItems[index];
        CanvasGroup cg = itemCanvasGroups[index];

        if (item == null || cg == null)
            yield break;

        Vector2 startPos = itemOriginalPositions[index] + new Vector2(0f, itemStartOffsetY);
        Vector2 endPos = itemOriginalPositions[index];

        item.anchoredPosition = startPos;
        cg.alpha = 0f;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, itemFadeDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            item.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            cg.alpha = smoothT;

            yield return null;
        }

        item.anchoredPosition = endPos;
        cg.alpha = 1f;
    }

    private void EnableMenuInteraction()
    {
        menuGroupCanvasGroup.alpha = 1f;
        menuGroupCanvasGroup.interactable = true;
        menuGroupCanvasGroup.blocksRaycasts = true;

        for (int i = 0; i < hoverScripts.Length; i++)
        {
            if (hoverScripts[i] != null)
                hoverScripts[i].SetHoverEnabled(true);
        }
    }

    private void EnableMenuEffects()
    {
        if (backgroundParallax != null)
            backgroundParallax.SetParallaxEnabled(true);

        if (atmosphereEffect != null)
            atmosphereEffect.EnableEffects();
    }

    private void DisableMenuEffects()
    {
        if (backgroundParallax != null)
            backgroundParallax.SetParallaxEnabled(false);

        if (atmosphereEffect != null)
            atmosphereEffect.DisableEffects();
    }

    private void ShowAllInstantly()
    {
        if (backgroundRect != null)
            backgroundRect.anchoredPosition = backgroundOriginalPosition;

        menuGroupCanvasGroup.alpha = 1f;
        menuGroupCanvasGroup.interactable = true;
        menuGroupCanvasGroup.blocksRaycasts = true;

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null || itemCanvasGroups[i] == null)
                continue;

            menuItems[i].anchoredPosition = itemOriginalPositions[i];
            itemCanvasGroups[i].alpha = 1f;

            if (hoverScripts[i] != null)
                hoverScripts[i].SetHoverEnabled(true);
        }

        if (audioSpectrum != null)
            audioSpectrum.ShowInstant();

        EnableMenuEffects();
    }

    public void StartGameWithMenuFade()
    {
        if (isStarting)
            return;

        isStarting = true;
        StartCoroutine(StartGameFlowRoutine());
    }

    private IEnumerator StartGameFlowRoutine()
    {
        DisableMenuEffects();

        menuGroupCanvasGroup.interactable = false;
        menuGroupCanvasGroup.blocksRaycasts = false;

        if (audioSpectrum != null)
            audioSpectrum.PlayFadeOut(menuFadeOutDuration);

        float elapsed = 0f;
        float startAlpha = menuGroupCanvasGroup.alpha;
        float safeDuration = Mathf.Max(0.01f, menuFadeOutDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            menuGroupCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            yield return null;
        }

        menuGroupCanvasGroup.alpha = 0f;

        if (mainMenuManager != null)
            mainMenuManager.StartGameWithLoading();
        else
            Debug.LogWarning("MainMenuManager reference is missing.");
    }
}
