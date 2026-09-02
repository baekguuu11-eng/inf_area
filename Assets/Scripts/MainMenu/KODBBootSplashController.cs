using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class KODBBootSplashController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string LogoResourcePath = "KODBLogo";

    private static bool hasPlayedThisSession;
    private static bool splashRunning;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 0.65f;
    [SerializeField] private float holdDuration = 0.95f;
    [SerializeField] private float fadeOutDuration = 0.65f;
    [SerializeField] private float blackHoldDuration = 0.18f;
    [SerializeField] private float menuStartDelay = 0.08f;

    public static bool ShouldDelayMainMenuIntro
    {
        get
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return !hasPlayedThisSession && activeScene.IsValid() && activeScene.name == MainMenuSceneName;
        }
    }


    /// <summary>
    /// 게임오버 화면 등에서 메인메뉴로 돌아올 때 부트 로고가 다시 메뉴를 가로막지 않도록 합니다.
    /// </summary>
    public static void SkipSplashForMenuReturn()
    {
        hasPlayedThisSession = true;
        splashRunning = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState()
    {
        hasPlayedThisSession = false;
        splashRunning = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != MainMenuSceneName)
            return;

        if (hasPlayedThisSession)
            return;

        KODBBootSplashController existing = Object.FindAnyObjectByType<KODBBootSplashController>();
        if (existing != null)
            return;

        GameObject controllerObject = new GameObject("KODBBootSplashController");
        controllerObject.AddComponent<KODBBootSplashController>();
    }

    private void Awake()
    {
        if (hasPlayedThisSession || splashRunning)
        {
            Destroy(gameObject);
            return;
        }

        splashRunning = true;
        StartCoroutine(PlaySplashRoutine());
    }

    private IEnumerator PlaySplashRoutine()
    {
        MainMenuIntroAnimator introAnimator = Object.FindAnyObjectByType<MainMenuIntroAnimator>();
        CanvasGroup overlayGroup = null;
        CanvasGroup logoGroup = null;
        GameObject overlayRoot = null;
        Sprite runtimeLogoSprite = null;

        try
        {
            overlayRoot = BuildOverlay(out overlayGroup, out logoGroup, out runtimeLogoSprite);
            if (overlayGroup == null)
                throw new MissingReferenceException("KODB splash CanvasGroup could not be created.");

            overlayGroup.alpha = 1f;
            overlayGroup.interactable = true;
            overlayGroup.blocksRaycasts = true;
            logoGroup.alpha = 0f;

            yield return FadeCanvasGroup(logoGroup, 0f, 1f, fadeInDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdDuration));
            yield return FadeCanvasGroup(logoGroup, 1f, 0f, fadeOutDuration);

            // 로고가 사라진 뒤 검은 화면만 잠시 유지해 부팅 연출의 여운을 줍니다.
            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);
        }
        finally
        {
            hasPlayedThisSession = true;
            splashRunning = false;

            if (overlayRoot != null)
                Destroy(overlayRoot);
            if (runtimeLogoSprite != null)
                Destroy(runtimeLogoSprite);
        }

        if (menuStartDelay > 0f)
            yield return new WaitForSecondsRealtime(menuStartDelay);

        if (introAnimator == null)
            introAnimator = Object.FindAnyObjectByType<MainMenuIntroAnimator>();

        if (introAnimator != null)
            introAnimator.BeginIntroAfterSplash();
        else
            Debug.LogWarning("[KODB Splash] MainMenuIntroAnimator를 찾지 못했습니다. 메뉴는 수동으로 확인해야 합니다.");

        Destroy(gameObject);
    }

    private static GameObject BuildOverlay(out CanvasGroup canvasGroup, out CanvasGroup logoGroup, out Sprite runtimeLogoSprite)
    {
        GameObject root = new GameObject("KODB_BootSplashRoot");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        canvasGroup = root.AddComponent<CanvasGroup>();

        GameObject blackObject = new GameObject("BlackBackground");
        blackObject.transform.SetParent(root.transform, false);
        Image blackImage = blackObject.AddComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = true;
        StretchToParent(blackImage.rectTransform);

        GameObject logoObject = new GameObject("KODBLogo");
        logoObject.transform.SetParent(root.transform, false);
        logoGroup = logoObject.AddComponent<CanvasGroup>();
        Image logoImage = logoObject.AddComponent<Image>();
        logoImage.color = Color.white;
        logoImage.raycastTarget = false;
        logoImage.preserveAspect = false;
        StretchToParent(logoImage.rectTransform);

        runtimeLogoSprite = null;
        Sprite importedLogo = Resources.Load<Sprite>(LogoResourcePath);
        if (importedLogo != null)
        {
            logoImage.sprite = importedLogo;
        }
        else
        {
            Texture2D logoTexture = Resources.Load<Texture2D>(LogoResourcePath);
            if (logoTexture != null)
            {
                runtimeLogoSprite = Sprite.Create(
                    logoTexture,
                    new Rect(0f, 0f, logoTexture.width, logoTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                runtimeLogoSprite.name = "KODBLogo_RuntimeSprite";
                logoImage.sprite = runtimeLogoSprite;
            }
            else
            {
                logoImage.enabled = false;
                Debug.LogWarning("[KODB Splash] Assets/Resources/KODBLogo.png를 불러오지 못했습니다.");
            }
        }

        return root;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            t = t * t * (3f - 2f * t);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }
}
