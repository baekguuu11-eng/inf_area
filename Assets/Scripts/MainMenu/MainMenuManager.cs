using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class MainMenuManager : MonoBehaviour
{
    [System.Serializable]
    public class LoadingStep
    {
        [Range(0f, 1f)] public float targetFillAmount = 0.5f;
        [Min(0f)] public float duration = 0.5f;
        [Min(0f)] public float waitAfterStep = 0f;
    }

    [System.Serializable]
    public class LoadingPattern
    {
        public string patternName = "Default Loading";
        public List<LoadingStep> steps = new List<LoadingStep>();
    }

    [System.Serializable]
    public class LoadingTip
    {
        [TextArea] public string message = "감염된 구역에 진입하는 중...";
        [Min(0.1f)] public float displayTime = 2f;
    }

    [Header("Scene")]
    [FormerlySerializedAs("gameSceneName")]
    [SerializeField] private string sceneNameAfterLoading = "OpeningCutscene";

    [Header("Main Menu References")]
    [SerializeField] private GameObject menuGroup;
    [SerializeField] private Button startButton;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingTipText;
    [SerializeField] private TextMeshProUGUI loadingPercentText;
    [SerializeField] private Image loadingBarFill;

    [Header("Loading Panel Visual")]
    [SerializeField] private bool keepMainBackgroundVisible = true;
    [SerializeField] private bool makeLoadingPanelBackgroundTransparent = true;

    [Header("Loading Tip Typewriter")]
    [SerializeField] private bool useLoadingTipTypewriter = true;
    [SerializeField] private float loadingTipCharacterInterval = 0.035f;

    [Header("Fade UI")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private bool useFinalFadeBeforeSceneChange = true;
    [SerializeField] private float fadeOutDuration = 0.7f;

    [Header("Loading Timing")]
    [SerializeField] private float minimumLoadingTime = 3f;
    [SerializeField] private float completeWaitTime = 0.7f;

    [Header("Final Message")]
    [SerializeField] private string finalLoadingMessage = "컷신 데이터를 불러오는 중...";

    [Header("Random Loading Patterns")]
    [SerializeField] private List<LoadingPattern> loadingPatterns = new List<LoadingPattern>();

    [Header("Random Loading Tips")]
    [SerializeField] private List<LoadingTip> loadingTips = new List<LoadingTip>();

    private bool isLoading = false;
    private bool stopTipLoop = false;
    private int lastTipIndex = -1;
    private float currentFillAmount = 0f;

    private Coroutine tipCoroutine;
    private Coroutine typingCoroutine;

    private void Reset()
    {
        SetDefaultLoadingPatterns();
        SetDefaultLoadingTips();
    }

    private void Awake()
    {
        AutoFindReferences();
        PrepareLoadingPanel();

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        SetLoadingFill(0f);

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.interactable = false;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        if (loadingPatterns == null || loadingPatterns.Count == 0)
            SetDefaultLoadingPatterns();

        if (loadingTips == null || loadingTips.Count == 0)
            SetDefaultLoadingTips();
    }

    private void AutoFindReferences()
    {
        if (menuGroup == null)
        {
            GameObject found = GameObject.Find("MenuGroup");
            if (found != null)
                menuGroup = found;
        }

        if (loadingPanel == null)
        {
            GameObject found = GameObject.Find("LoadingPanel");
            if (found != null)
                loadingPanel = found;
        }
    }

    private void PrepareLoadingPanel()
    {
        if (loadingPanel == null)
            return;

        RectTransform rt = loadingPanel.GetComponent<RectTransform>();

        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        if (makeLoadingPanelBackgroundTransparent)
        {
            Image panelImage = loadingPanel.GetComponent<Image>();

            if (panelImage != null)
            {
                Color c = panelImage.color;
                c.a = 0f;
                panelImage.color = c;
                panelImage.raycastTarget = false;
            }
        }
    }

    public void StartGameWithLoading()
    {
        if (isLoading)
            return;

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        isLoading = true;
        stopTipLoop = false;
        lastTipIndex = -1;

        if (startButton != null)
            startButton.interactable = false;

        // 메뉴 버튼만 숨김. 배경은 건드리지 않음.
        if (menuGroup != null)
            menuGroup.SetActive(false);

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            loadingPanel.transform.SetAsLastSibling();
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.interactable = false;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        SetLoadingFill(0f);

        if (loadingTipText != null)
            loadingTipText.text = "";

        tipCoroutine = StartCoroutine(RandomTipLoop());

        AsyncOperation sceneLoadOperation = SceneManager.LoadSceneAsync(sceneNameAfterLoading);

        if (sceneLoadOperation == null)
        {
            Debug.LogError("[MainMenuManager] 씬 로딩 실패: " + sceneNameAfterLoading +
                           "\nBuild Profiles / Build Settings에 이 씬이 등록되어 있는지 확인하세요.");

            if (tipCoroutine != null)
                StopCoroutine(tipCoroutine);

            if (typingCoroutine != null)
                StopCoroutine(typingCoroutine);

            if (loadingTipText != null)
                loadingTipText.text = "씬 로딩 실패: Build Settings 확인 필요";

            if (startButton != null)
                startButton.interactable = true;

            isLoading = false;
            yield break;
        }

        sceneLoadOperation.allowSceneActivation = false;

        float loadingStartTime = Time.unscaledTime;
        List<LoadingStep> selectedSteps = GetRandomLoadingSteps();

        for (int i = 0; i < selectedSteps.Count; i++)
        {
            LoadingStep step = selectedSteps[i];

            float startFill = currentFillAmount;
            float targetFill = Mathf.Clamp01(step.targetFillAmount);
            float duration = Mathf.Max(0.01f, step.duration);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float nextFill = Mathf.Lerp(startFill, targetFill, t);

                SetLoadingFill(nextFill);

                yield return null;
            }

            SetLoadingFill(targetFill);

            if (step.waitAfterStep > 0f)
                yield return new WaitForSecondsRealtime(step.waitAfterStep);
        }

        while (sceneLoadOperation.progress < 0.9f)
            yield return null;

        float loadingElapsed = Time.unscaledTime - loadingStartTime;

        if (loadingElapsed < minimumLoadingTime)
            yield return new WaitForSecondsRealtime(minimumLoadingTime - loadingElapsed);

        SetLoadingFill(1f);

        stopTipLoop = true;

        if (tipCoroutine != null)
        {
            StopCoroutine(tipCoroutine);
            tipCoroutine = null;
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (loadingTipText != null)
        {
            if (useLoadingTipTypewriter)
                yield return TypeLoadingText(string.IsNullOrWhiteSpace(finalLoadingMessage) ? "컷신 데이터를 불러오는 중..." : finalLoadingMessage);
            else
                loadingTipText.text = string.IsNullOrWhiteSpace(finalLoadingMessage) ? "컷신 데이터를 불러오는 중..." : finalLoadingMessage;
        }

        if (loadingPercentText != null)
            loadingPercentText.text = "100%";

        yield return new WaitForSecondsRealtime(completeWaitTime);

        if (useFinalFadeBeforeSceneChange)
            yield return FadeOut();

        sceneLoadOperation.allowSceneActivation = true;
    }

    private IEnumerator RandomTipLoop()
    {
        while (!stopTipLoop)
        {
            LoadingTip tip = GetRandomTip();

            if (loadingTipText != null)
            {
                if (typingCoroutine != null)
                    StopCoroutine(typingCoroutine);

                if (useLoadingTipTypewriter)
                    typingCoroutine = StartCoroutine(TypeLoadingText(tip.message));
                else
                    loadingTipText.text = tip.message;
            }

            float displayTime = Mathf.Max(0.1f, tip.displayTime);
            float elapsed = 0f;

            while (elapsed < displayTime)
            {
                if (stopTipLoop)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator TypeLoadingText(string message)
    {
        if (loadingTipText == null)
            yield break;

        if (string.IsNullOrEmpty(message))
        {
            loadingTipText.text = "";
            yield break;
        }

        loadingTipText.text = "";

        float interval = Mathf.Max(0f, loadingTipCharacterInterval);

        for (int i = 0; i < message.Length; i++)
        {
            loadingTipText.text = message.Substring(0, i + 1);

            if (interval > 0f)
                yield return new WaitForSecondsRealtime(interval);
            else
                yield return null;
        }
    }

    private LoadingTip GetRandomTip()
    {
        if (loadingTips == null || loadingTips.Count == 0)
        {
            return new LoadingTip
            {
                message = "감염된 구역에 진입하는 중...",
                displayTime = 2f
            };
        }

        if (loadingTips.Count == 1)
        {
            lastTipIndex = 0;
            return loadingTips[0];
        }

        int index = Random.Range(0, loadingTips.Count);
        int safetyCount = 0;

        while (index == lastTipIndex && safetyCount < 30)
        {
            index = Random.Range(0, loadingTips.Count);
            safetyCount++;
        }

        lastTipIndex = index;
        return loadingTips[index];
    }

    private IEnumerator FadeOut()
    {
        if (fadeCanvasGroup == null)
            yield break;

        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.transform.SetAsLastSibling();
        fadeCanvasGroup.interactable = true;
        fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeOutDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            fadeCanvasGroup.alpha = t;

            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    private void SetLoadingFill(float value)
    {
        currentFillAmount = Mathf.Clamp01(value);

        if (loadingBarFill != null)
            loadingBarFill.fillAmount = currentFillAmount;

        if (loadingPercentText != null)
        {
            int percent = Mathf.RoundToInt(currentFillAmount * 100f);
            loadingPercentText.text = percent + "%";
        }
    }

    private List<LoadingStep> GetRandomLoadingSteps()
    {
        if (loadingPatterns == null || loadingPatterns.Count == 0)
            SetDefaultLoadingPatterns();

        int patternIndex = Random.Range(0, loadingPatterns.Count);
        LoadingPattern selectedPattern = loadingPatterns[patternIndex];

        if (selectedPattern == null || selectedPattern.steps == null || selectedPattern.steps.Count == 0)
        {
            SetDefaultLoadingPatterns();
            selectedPattern = loadingPatterns[0];
        }

        return selectedPattern.steps;
    }

    private void SetDefaultLoadingPatterns()
    {
        loadingPatterns = new List<LoadingPattern>
        {
            new LoadingPattern
            {
                patternName = "Default Loading",
                steps = new List<LoadingStep>
                {
                    new LoadingStep { targetFillAmount = 0.12f, duration = 0.35f, waitAfterStep = 0.05f },
                    new LoadingStep { targetFillAmount = 0.36f, duration = 0.55f, waitAfterStep = 0.10f },
                    new LoadingStep { targetFillAmount = 0.62f, duration = 0.45f, waitAfterStep = 0.05f },
                    new LoadingStep { targetFillAmount = 0.84f, duration = 0.55f, waitAfterStep = 0.15f },
                    new LoadingStep { targetFillAmount = 1.00f, duration = 0.45f, waitAfterStep = 0.00f }
                }
            },
            new LoadingPattern
            {
                patternName = "Interrupted Signal",
                steps = new List<LoadingStep>
                {
                    new LoadingStep { targetFillAmount = 0.18f, duration = 0.25f, waitAfterStep = 0.20f },
                    new LoadingStep { targetFillAmount = 0.31f, duration = 0.50f, waitAfterStep = 0.10f },
                    new LoadingStep { targetFillAmount = 0.57f, duration = 0.35f, waitAfterStep = 0.25f },
                    new LoadingStep { targetFillAmount = 0.79f, duration = 0.45f, waitAfterStep = 0.10f },
                    new LoadingStep { targetFillAmount = 1.00f, duration = 0.60f, waitAfterStep = 0.00f }
                }
            },
            new LoadingPattern
            {
                patternName = "Fast Entry",
                steps = new List<LoadingStep>
                {
                    new LoadingStep { targetFillAmount = 0.25f, duration = 0.25f, waitAfterStep = 0.05f },
                    new LoadingStep { targetFillAmount = 0.50f, duration = 0.35f, waitAfterStep = 0.05f },
                    new LoadingStep { targetFillAmount = 0.75f, duration = 0.35f, waitAfterStep = 0.10f },
                    new LoadingStep { targetFillAmount = 1.00f, duration = 0.50f, waitAfterStep = 0.00f }
                }
            }
        };
    }

    private void SetDefaultLoadingTips()
    {
        loadingTips = new List<LoadingTip>
        {
            new LoadingTip { message = "감염된 구역에 진입하는 중...", displayTime = 2f },
            new LoadingTip { message = "방화벽 우회 경로를 계산하는 중...", displayTime = 2f },
            new LoadingTip { message = "라파엘 백신 데이터를 준비하는 중...", displayTime = 2f },
            new LoadingTip { message = "아크 중심부 접속을 대기하는 중...", displayTime = 2f },
            new LoadingTip { message = "적을 처치하면 방을 정화할 수 있습니다.", displayTime = 2f },
            new LoadingTip { message = "경고 표식이 보인다면 신중하게 접근해야 합니다.", displayTime = 2f }
        };
    }

    private void OnValidate()
    {
        minimumLoadingTime = Mathf.Max(0f, minimumLoadingTime);
        completeWaitTime = Mathf.Max(0f, completeWaitTime);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        loadingTipCharacterInterval = Mathf.Max(0f, loadingTipCharacterInterval);

        if (loadingPatterns == null)
            return;

        for (int i = 0; i < loadingPatterns.Count; i++)
        {
            if (loadingPatterns[i] == null || loadingPatterns[i].steps == null)
                continue;

            for (int j = 0; j < loadingPatterns[i].steps.Count; j++)
            {
                LoadingStep step = loadingPatterns[i].steps[j];

                if (step == null)
                    continue;

                step.targetFillAmount = Mathf.Clamp01(step.targetFillAmount);
                step.duration = Mathf.Max(0f, step.duration);
                step.waitAfterStep = Mathf.Max(0f, step.waitAfterStep);
            }
        }
    }
}