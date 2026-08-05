using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class DeathScreenUI : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Fade")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.45f;
    [SerializeField] private float finalAlpha = 1f;

    [Header("Background")]
    [SerializeField] private Image deathBackgroundImage;
    [Range(0f, 1f)]
    [SerializeField] private float backgroundImageAlpha = 0.92f;

    [Header("Hide UI On Death")]
    [SerializeField] private GameObject[] hideOnDeath;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        // 데스 패널이 항상 제일 위로 오게 함
        transform.SetAsLastSibling();

        HideGameplayUI();
        HideHeartImagesDirectly();
        PrepareButtonVisuals();
        PrepareBackgroundAlpha();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeInRoutine());
    }

    private void HideGameplayUI()
    {
        if (hideOnDeath == null)
        {
            return;
        }

        for (int i = 0; i < hideOnDeath.Length; i++)
        {
            if (hideOnDeath[i] != null)
            {
                hideOnDeath[i].SetActive(false);
            }
        }
    }

    private void HideHeartImagesDirectly()
    {
#if UNITY_2023_1_OR_NEWER
        HeartUI[] heartUIs = FindObjectsByType<HeartUI>(FindObjectsInactive.Include);
#else
        HeartUI[] heartUIs = FindObjectsOfType<HeartUI>(true);
#endif

        for (int i = 0; i < heartUIs.Length; i++)
        {
            if (heartUIs[i] == null)
            {
                continue;
            }

            HideImageArray(heartUIs[i].hearts);
            HideImageArray(heartUIs[i].bonusHearts);
        }
    }

    private void HideImageArray(Image[] images)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
            {
                images[i].gameObject.SetActive(false);
            }
        }
    }

    private void PrepareBackgroundAlpha()
    {
        if (deathBackgroundImage == null)
        {
            return;
        }

        Color color = deathBackgroundImage.color;
        color.a = backgroundImageAlpha;
        deathBackgroundImage.color = color;

        deathBackgroundImage.raycastTarget = false;
    }

    private void PrepareButtonVisuals()
    {
        MakeButtonTransparent(restartButton);
        MakeButtonTransparent(mainMenuButton);
    }

    private void MakeButtonTransparent(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image buttonImage = button.GetComponent<Image>();

        if (buttonImage != null)
        {
            Color color = buttonImage.color;
            color.a = 0f;
            buttonImage.color = color;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);

        if (legacyText != null)
        {
            legacyText.gameObject.SetActive(false);
        }

        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);

        if (tmpText != null)
        {
            tmpText.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeInRoutine()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(0f, finalAlpha, t);

            yield return null;
        }

        canvasGroup.alpha = finalAlpha;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        fadeCoroutine = null;
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}