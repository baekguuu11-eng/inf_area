using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuStartCutsceneButton : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string cutsceneSceneName = "OpeningCutscene";

    [Header("Button")]
    [SerializeField] private Button startButton;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private Image loadingBarImage;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI loadingPercentText;

    [Header("Timing")]
    [SerializeField] private float minimumLoadingTime = 1.5f;

    private bool isLoading = false;

    private void Awake()
    {
        if (startButton == null)
            startButton = GetComponent<Button>();

        if (startButton != null)
            startButton.onClick.AddListener(StartLoadingCutscene);

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        SetLoadingProgress(0f);
    }

    public void StartLoadingCutscene()
    {
        if (isLoading)
            return;

        StartCoroutine(LoadCutsceneRoutine());
    }

    private IEnumerator LoadCutsceneRoutine()
    {
        isLoading = true;

        if (startButton != null)
            startButton.interactable = false;

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        SetLoadingProgress(0f);

        AsyncOperation operation = SceneManager.LoadSceneAsync(cutsceneSceneName);

        if (operation == null)
        {
            Debug.LogError(
                "[MainMenuStartCutsceneButton] 컷신 씬 로딩 실패. " +
                "Build Profiles 또는 Build Settings에 '" + cutsceneSceneName + "' 씬을 추가했는지 확인하세요."
            );

            isLoading = false;

            if (startButton != null)
                startButton.interactable = true;

            yield break;
        }

        operation.allowSceneActivation = false;

        float elapsed = 0f;

        while (elapsed < minimumLoadingTime || operation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            float timeProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, minimumLoadingTime));
            float loadProgress = Mathf.Clamp01(operation.progress / 0.9f);

            float progress = Mathf.Min(timeProgress, loadProgress);

            SetLoadingProgress(progress);

            yield return null;
        }

        SetLoadingProgress(1f);

        yield return new WaitForSecondsRealtime(0.25f);

        operation.allowSceneActivation = true;
    }

    private void SetLoadingProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (loadingSlider != null)
            loadingSlider.value = progress;

        if (loadingBarImage != null)
            loadingBarImage.fillAmount = progress;

        string percent = Mathf.RoundToInt(progress * 100f) + "%";

        if (loadingText != null)
            loadingText.text = percent;

        if (loadingPercentText != null)
            loadingPercentText.text = percent;
    }
}