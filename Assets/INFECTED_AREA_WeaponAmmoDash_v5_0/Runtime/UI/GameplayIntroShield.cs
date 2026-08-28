using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Covers only the gameplay world and gameplay HUD during the boot intro.
/// GameIntroCanvas is forced above this shield so FadePanel and SyncText remain visible.
/// </summary>
[DefaultExecutionOrder(-32000)]
public sealed class GameplayIntroShield : MonoBehaviour
{
    public static bool GameplayVisible { get; private set; } = true;

    private const int IntroCanvasOrder = 32000;
    private const int ShieldCanvasOrder = 31990;

    private Image blackImage;
    private object introManager;
    private FieldInfo finishingField;
    private FieldInfo fadeField;
    private float noIntroTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        GameplayVisible = false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string lower = scene.name.ToLowerInvariant();
        if (!lower.Contains("gamescene"))
        {
            GameplayVisible = true;
            return;
        }

        GameplayVisible = false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameplayIntroShield existing = root.GetComponent<GameplayIntroShield>();
            if (existing != null) return;
        }

        GameObject shieldObject = new GameObject("GameplayIntroShield");
        SceneManager.MoveGameObjectToScene(shieldObject, scene);
        shieldObject.AddComponent<GameplayIntroShield>();
    }

    private void Awake()
    {
        GameplayVisible = false;
        PromoteIntroCanvas();
        BuildShieldCanvas();
    }

    private void Start()
    {
        FindIntroManager();
    }

    private void LateUpdate()
    {
        PromoteIntroCanvas();

        if (introManager == null)
        {
            FindIntroManager();
            noIntroTimer += Time.unscaledDeltaTime;
            if (noIntroTimer > 2f)
                Release();
            return;
        }

        bool finishing = finishingField != null && (bool)finishingField.GetValue(introManager);
        if (!finishing)
        {
            SetShieldAlpha(1f);
            return;
        }

        Image fade = fadeField != null ? fadeField.GetValue(introManager) as Image : null;
        float alpha = fade != null ? fade.color.a : 0f;
        SetShieldAlpha(alpha);

        if (alpha <= 0.002f)
            Release();
    }

    private void PromoteIntroCanvas()
    {
        GameObject introCanvasObject = GameObject.Find("GameIntroCanvas");
        if (introCanvasObject == null) return;

        Canvas introCanvas = introCanvasObject.GetComponent<Canvas>();
        if (introCanvas != null)
        {
            introCanvas.overrideSorting = true;
            introCanvas.sortingOrder = IntroCanvasOrder;
        }

        // FadePanel must render first and the text must render above it.
        Transform syncText = FindChildRecursive(introCanvasObject.transform, "SyncText");
        if (syncText != null)
        {
            syncText.gameObject.SetActive(true);
            syncText.SetAsLastSibling();
        }

        CanvasGroup group = introCanvasObject.GetComponent<CanvasGroup>();
        if (group != null && !IsIntroFinishing())
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }
    }

    private void BuildShieldCanvas()
    {
        GameObject canvasObject = new GameObject("WorldBlockCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = ShieldCanvasOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("BlackWorldShield", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        blackImage = panelObject.GetComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
    }

    private void FindIntroManager()
    {
        System.Type type = System.Type.GetType("GameSceneIntroManager, Assembly-CSharp");
        if (type == null) return;

        Object[] all = Resources.FindObjectsOfTypeAll(type);
        for (int i = 0; i < all.Length; i++)
        {
            Component component = all[i] as Component;
            if (component == null || !component.gameObject.scene.isLoaded) continue;

            introManager = all[i];
            finishingField = type.GetField("isFinishing", BindingFlags.Instance | BindingFlags.NonPublic);
            fadeField = type.GetField("fadePanel", BindingFlags.Instance | BindingFlags.NonPublic);
            if (finishingField == null || fadeField == null)
                introManager = null;
            break;
        }
    }

    private bool IsIntroFinishing()
    {
        return introManager != null && finishingField != null && (bool)finishingField.GetValue(introManager);
    }

    private void SetShieldAlpha(float alpha)
    {
        if (blackImage == null) return;
        Color color = blackImage.color;
        color.a = Mathf.Clamp01(alpha);
        blackImage.color = color;
    }

    private void Release()
    {
        GameplayVisible = true;
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null) return result;
        }

        return null;
    }
}
