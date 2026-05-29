using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [System.Serializable]
    public class LoadingStep
    {
        [Range(0f, 1f)]
        public float targetFillAmount = 0.5f;

        [Min(0f)]
        public float duration = 0.5f;

        [Min(0f)]
        public float waitAfterStep = 0f;
    }

    [System.Serializable]
    public class LoadingPattern
    {
        public string patternName = "기본 로딩 패턴";
        public List<LoadingStep> steps = new List<LoadingStep>();
    }

    [System.Serializable]
    public class LoadingTip
    {
        [TextArea]
        public string message = "Entering infected area...";

        [Min(0.1f)]
        public float displayTime = 2f;
    }

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Main Menu References")]
    [SerializeField] private GameObject menuGroup;
    [SerializeField] private Button startButton;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingTipText;
    [SerializeField] private TextMeshProUGUI loadingPercentText;
    [SerializeField] private Image loadingBarFill;

    [Header("Fade UI")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeOutDuration = 0.7f;

    [Header("Loading Timing")]
    [SerializeField] private float minimumLoadingTime = 3f;
    [SerializeField] private float completeWaitTime = 0.7f;

    [Header("Final Message")]
    [SerializeField] private string finalLoadingMessage = "Ready to enter infected area";

    [Header("Random Loading Patterns")]
    [SerializeField] private List<LoadingPattern> loadingPatterns = new List<LoadingPattern>();

    [Header("Random Loading Tips")]
    [SerializeField] private List<LoadingTip> loadingTips = new List<LoadingTip>();

    private bool isLoading = false;
    private bool stopTipLoop = false;
    private int lastTipIndex = -1;
    private float currentFillAmount = 0f;

    private Coroutine tipCoroutine;

    private void Reset()
    {
        SetDefaultLoadingPatterns();
        SetDefaultLoadingTips();
    }

    private void Awake()
    {
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

        if (menuGroup != null)
            menuGroup.SetActive(false);

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        SetLoadingFill(0f);

        tipCoroutine = StartCoroutine(RandomTipLoop());

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

        SetLoadingFill(1f);

        float loadingElapsed = Time.unscaledTime - loadingStartTime;

        if (loadingElapsed < minimumLoadingTime)
        {
            float remainTime = minimumLoadingTime - loadingElapsed;
            yield return new WaitForSecondsRealtime(remainTime);
        }

        stopTipLoop = true;

        if (tipCoroutine != null)
        {
            StopCoroutine(tipCoroutine);
            tipCoroutine = null;
        }

        if (loadingTipText != null)
        {
            if (string.IsNullOrWhiteSpace(finalLoadingMessage))
                loadingTipText.text = "Ready to enter infected area";
            else
                loadingTipText.text = finalLoadingMessage;
        }

        if (loadingPercentText != null)
            loadingPercentText.text = "100%";

        yield return new WaitForSecondsRealtime(completeWaitTime);

        yield return FadeOut();

        SceneManager.LoadScene(gameSceneName);
    }

    private IEnumerator RandomTipLoop()
    {
        while (!stopTipLoop)
        {
            LoadingTip tip = GetRandomTip();

            if (loadingTipText != null)
                loadingTipText.text = tip.message;

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

    private LoadingTip GetRandomTip()
    {
        if (loadingTips == null || loadingTips.Count == 0)
        {
            return new LoadingTip
            {
                message = "Entering infected area...",
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
                patternName = "Fast Entry",
                steps = new List<LoadingStep>
                {
                    new LoadingStep { targetFillAmount = 0.45f, duration = 0.4f, waitAfterStep = 0f },
                    new LoadingStep { targetFillAmount = 0.72f, duration = 0.8f, waitAfterStep = 0f },
                    new LoadingStep { targetFillAmount = 0.96f, duration = 1.2f, waitAfterStep = 0.3f },
                    new LoadingStep { targetFillAmount = 1.0f, duration = 0.25f, waitAfterStep = 0f }
                }
            },
            new LoadingPattern
            {
                patternName = "Stuck At 99",
                steps = new List<LoadingStep>
                {
                    new LoadingStep { targetFillAmount = 0.30f, duration = 0.35f, waitAfterStep = 0f },
                    new LoadingStep { targetFillAmount = 0.62f, duration = 0.9f, waitAfterStep = 0f },
                    new LoadingStep { targetFillAmount = 0.91f, duration = 1.1f, waitAfterStep = 0f },
                    new LoadingStep { targetFillAmount = 0.99f, duration = 1.0f, waitAfterStep = 0.8f },
                    new LoadingStep { targetFillAmount = 1.0f, duration = 0.35f, waitAfterStep = 0f }
                }
            },
            new LoadingPattern
            {
                patternName = "Middle Delay",
                steps = new List<LoadingStep>
                {
                    new LoadingStep { targetFillAmount = 0.18f, duration = 0.25f, waitAfterStep = 0f },
                    new LoadingStep { targetFillAmount = 0.50f, duration = 1.1f, waitAfterStep = 0.4f },
                    new LoadingStep { targetFillAmount = 0.74f, duration = 0.8f, waitAfterStep = 0f },
                    new LoadingStep { targetFillAmount = 0.97f, duration = 1.4f, waitAfterStep = 0.3f },
                    new LoadingStep { targetFillAmount = 1.0f, duration = 0.4f, waitAfterStep = 0f }
                }
            },
            new LoadingPattern
            {
                patternName = "Step Loading",
                steps = new List<LoadingStep>
                {
                    new LoadingStep { targetFillAmount = 0.22f, duration = 0.3f, waitAfterStep = 0.15f },
                    new LoadingStep { targetFillAmount = 0.38f, duration = 0.35f, waitAfterStep = 0.2f },
                    new LoadingStep { targetFillAmount = 0.57f, duration = 0.45f, waitAfterStep = 0.15f },
                    new LoadingStep { targetFillAmount = 0.79f, duration = 0.8f, waitAfterStep = 0.25f },
                    new LoadingStep { targetFillAmount = 0.99f, duration = 1.1f, waitAfterStep = 0.5f },
                    new LoadingStep { targetFillAmount = 1.0f, duration = 0.25f, waitAfterStep = 0f }
                }
            }
        };
    }

    private void SetDefaultLoadingTips()
    {
        loadingTips = new List<LoadingTip>
    {
        new LoadingTip { message = "이 게임은 약 9개월 동안 개발되었습니다.", displayTime = 2f },
        new LoadingTip { message = "주인공의 이름은 라파엘입니다.", displayTime = 2f },
        new LoadingTip { message = "라파엘은 감염된 구역을 정화하기 위해 투입되었습니다.", displayTime = 2f },
        new LoadingTip { message = "감염된 구역의 모든 게이트가 안전한 것은 아닙니다.", displayTime = 2f },
        new LoadingTip { message = "적을 처치하면 방을 정화할 수 있습니다.", displayTime = 2f },
        new LoadingTip { message = "일부 적은 원거리 공격을 사용합니다.", displayTime = 2f },
        new LoadingTip { message = "포탈방은 다음 스테이지로 이어지는 핵심 지점입니다.", displayTime = 2f },
        new LoadingTip { message = "각 스테이지는 일반 방과 포탈방으로 구성됩니다.", displayTime = 2f },
        new LoadingTip { message = "라파엘의 검은 감염체를 베어내기 위해 제작되었습니다.", displayTime = 2f },
        new LoadingTip { message = "붉은 경고등은 감염 위험 구역을 의미합니다.", displayTime = 2f },
        new LoadingTip { message = "게이트를 통과하면 새로운 방이 열립니다.", displayTime = 2f },
        new LoadingTip { message = "감염도 시스템은 추후 게임 난이도에 영향을 줄 예정입니다.", displayTime = 2f },
        new LoadingTip { message = "감염체들은 종류에 따라 서로 다른 방식으로 공격합니다.", displayTime = 2f },
        new LoadingTip { message = "게이트 너머에는 새로운 위험이 기다리고 있습니다.", displayTime = 2f },
        new LoadingTip { message = "격리 시스템이 실패한 뒤 감염은 빠르게 확산되었습니다.", displayTime = 2f },
        new LoadingTip { message = "라파엘은 마지막 백신 실험체입니다.", displayTime = 2f },
        new LoadingTip { message = "포탈은 아직 정화되지 않은 구역으로 이어질 수 있습니다.", displayTime = 2f },
        new LoadingTip { message = "경고 표식이 보인다면 신중하게 접근해야 합니다.", displayTime = 2f },
        new LoadingTip { message = "바이러스형 적은 단순하지만 빠르게 접근할 수 있습니다.", displayTime = 2f },
        new LoadingTip { message = "기계형 감염체는 예측하기 어려운 움직임을 보일 수 있습니다.", displayTime = 2f }
    };
    }

    private void OnValidate()
    {
        minimumLoadingTime = Mathf.Max(0f, minimumLoadingTime);
        completeWaitTime = Mathf.Max(0f, completeWaitTime);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

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