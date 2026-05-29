using System.Collections;
using UnityEngine;

public class MainMenuIntroAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup menuGroupCanvasGroup;
    [SerializeField] private RectTransform[] menuItems;
    [SerializeField] private MainMenuManager mainMenuManager;

    [Header("Intro Animation")]
    [SerializeField] private bool playIntroOnStart = true;
    [SerializeField] private float introStartDelay = 0.2f;
    [SerializeField] private float itemStartOffsetY = -18f;
    [SerializeField] private float itemFadeDuration = 0.16f;
    [SerializeField] private float itemDelayAfterComplete = 0.04f;

    [Header("Start Click Fade")]
    [SerializeField] private float menuFadeOutDuration = 0.2f;

    private Vector2[] originalPositions;
    private CanvasGroup[] itemCanvasGroups;
    private bool isStarting = false;

    private void Awake()
    {
        if (menuGroupCanvasGroup == null)
            menuGroupCanvasGroup = GetComponent<CanvasGroup>();

        if (menuGroupCanvasGroup == null)
            menuGroupCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheItems();
        PrepareIntroState();
    }

    private void Start()
    {
        if (playIntroOnStart)
            StartCoroutine(PlayIntroRoutine());
        else
            ShowAllInstantly();
    }

    private void CacheItems()
    {
        if (menuItems == null || menuItems.Length == 0)
        {
            menuItems = new RectTransform[transform.childCount];

            for (int i = 0; i < transform.childCount; i++)
                menuItems[i] = transform.GetChild(i).GetComponent<RectTransform>();
        }

        originalPositions = new Vector2[menuItems.Length];
        itemCanvasGroups = new CanvasGroup[menuItems.Length];

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null)
                continue;

            originalPositions[i] = menuItems[i].anchoredPosition;

            CanvasGroup cg = menuItems[i].GetComponent<CanvasGroup>();
            if (cg == null)
                cg = menuItems[i].gameObject.AddComponent<CanvasGroup>();

            itemCanvasGroups[i] = cg;
        }
    }

    private void PrepareIntroState()
    {
        menuGroupCanvasGroup.alpha = 1f;
        menuGroupCanvasGroup.interactable = false;
        menuGroupCanvasGroup.blocksRaycasts = false;

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null || itemCanvasGroups[i] == null)
                continue;

            itemCanvasGroups[i].alpha = 0f;
            menuItems[i].anchoredPosition = originalPositions[i] + new Vector2(0f, itemStartOffsetY);
        }
    }

    private IEnumerator PlayIntroRoutine()
    {
        // Unity가 첫 프레임에서 TMP, 이미지, UI를 준비할 시간을 조금 준다.
        yield return null;
        yield return new WaitForSecondsRealtime(introStartDelay);

        menuGroupCanvasGroup.alpha = 1f;
        menuGroupCanvasGroup.interactable = false;
        menuGroupCanvasGroup.blocksRaycasts = false;

        // 병렬 실행이 아니라 하나씩 끝난 뒤 다음 버튼 등장
        for (int i = 0; i < menuItems.Length; i++)
        {
            yield return StartCoroutine(AnimateItemIn(i));

            if (itemDelayAfterComplete > 0f)
                yield return new WaitForSecondsRealtime(itemDelayAfterComplete);
        }

        menuGroupCanvasGroup.interactable = true;
        menuGroupCanvasGroup.blocksRaycasts = true;
    }

    private IEnumerator AnimateItemIn(int index)
    {
        if (index < 0 || index >= menuItems.Length)
            yield break;

        RectTransform item = menuItems[index];
        CanvasGroup cg = itemCanvasGroups[index];

        if (item == null || cg == null)
            yield break;

        Vector2 startPos = originalPositions[index] + new Vector2(0f, itemStartOffsetY);
        Vector2 endPos = originalPositions[index];

        item.anchoredPosition = startPos;
        cg.alpha = 0f;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, itemFadeDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            // 부드럽게 감속
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            item.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            cg.alpha = smoothT;

            yield return null;
        }

        item.anchoredPosition = endPos;
        cg.alpha = 1f;
    }

    private void ShowAllInstantly()
    {
        menuGroupCanvasGroup.alpha = 1f;
        menuGroupCanvasGroup.interactable = true;
        menuGroupCanvasGroup.blocksRaycasts = true;

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null || itemCanvasGroups[i] == null)
                continue;

            menuItems[i].anchoredPosition = originalPositions[i];
            itemCanvasGroups[i].alpha = 1f;
        }
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
        menuGroupCanvasGroup.interactable = false;
        menuGroupCanvasGroup.blocksRaycasts = false;

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