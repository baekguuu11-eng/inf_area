using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuSubPanelManager : MonoBehaviour
{
    private enum PanelState
    {
        None,
        Settings,
        Makers,
        QuitConfirm
    }

    [Header("Main Menu")]
    [SerializeField] private CanvasGroup menuGroupCanvasGroup;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button makersButton;
    [SerializeField] private Button quitButton;

    [Header("Dim")]
    [SerializeField] private CanvasGroup backgroundDimCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float backgroundDimAlpha = 0.48f;

    [Header("Panels")]
    [SerializeField] private CanvasGroup settingsPanelCanvasGroup;
    [SerializeField] private CanvasGroup makersPanelCanvasGroup;
    [SerializeField] private CanvasGroup quitPanelCanvasGroup;

    [Header("Panel Buttons")]
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button makersBackButton;
    [SerializeField] private Button quitConfirmButton;
    [SerializeField] private Button quitCancelButton;
    [SerializeField] private Button resetSettingsButton;

    [Header("Transition")]
    [SerializeField] private float menuFadeDuration = 0.16f;
    [SerializeField] private float fallbackPanelFadeDuration = 0.18f;
    [SerializeField] private float fallbackPanelStartScale = 0.96f;

    [Header("Makers Fallback")]
    [Tooltip("패널 생성 애니메이터가 없을 때만 사용됩니다.")]
    [SerializeField] private CanvasGroup[] makerRows;
    [SerializeField] private float makerRowFadeDuration = 0.09f;
    [SerializeField] private float makerRowDelay = 0.045f;

    [Header("Settings - Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeValueText;
    [SerializeField] private TextMeshProUGUI bgmVolumeValueText;
    [SerializeField] private TextMeshProUGUI sfxVolumeValueText;
    [SerializeField] private AudioSource menuBgmAudioSource;

    [Header("Settings - Display")]
    [Tooltip("구형 패널에서 사용하는 Dropdown. 비어 있어도 안전합니다.")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Tooltip("현재 자동 생성 패널의 이전 해상도 버튼")]
    [SerializeField] private Button resolutionPreviousButton;

    [Tooltip("현재 자동 생성 패널의 다음 해상도 버튼")]
    [SerializeField] private Button resolutionNextButton;

    [Tooltip("현재 자동 생성 패널의 해상도 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI resolutionValueText;

    [SerializeField] private Toggle fullscreenToggle;

    [Header("Settings Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultBgmVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.8f;
    [SerializeField] private bool defaultFullscreen = true;

    private const string MasterVolumeKey = "IA_MASTER_VOLUME";
    private const string BgmVolumeKey = "IA_BGM_VOLUME";
    private const string SfxVolumeKey = "IA_SFX_VOLUME";
    private const string FullscreenKey = "IA_FULLSCREEN";
    private const string ResolutionWidthKey = "IA_RESOLUTION_WIDTH";
    private const string ResolutionHeightKey = "IA_RESOLUTION_HEIGHT";

    private PanelState currentPanel = PanelState.None;
    private bool isTransitioning;

    private MenuTextButtonHover[] mainMenuHoverScripts;
    private Coroutine transitionCoroutine;
    private Coroutine makersRevealCoroutine;

    private readonly List<Resolution> availableResolutions =
        new List<Resolution>();

    private int currentResolutionIndex;

    private void Awake()
    {
        AutoFindMenuHoverScripts();
        BuildResolutionList();
        PreparePanels();
        SetupButtonListeners();
        SetupSettingListeners();
        PopulateLegacyResolutionDropdown();
        LoadAndApplySettings();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        RemoveSettingListeners();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) &&
            currentPanel != PanelState.None &&
            !isTransitioning)
        {
            CloseCurrentPanel();
        }
    }

    private void AutoFindMenuHoverScripts()
    {
        if (menuGroupCanvasGroup != null)
        {
            mainMenuHoverScripts =
                menuGroupCanvasGroup.GetComponentsInChildren
                <MenuTextButtonHover>(true);
        }
        else
        {
            mainMenuHoverScripts =
                new MenuTextButtonHover[0];
        }
    }

    private void PreparePanels()
    {
        SetCanvasGroupInstant(
            menuGroupCanvasGroup,
            1f,
            true,
            true
        );

        PrepareHiddenPanel(settingsPanelCanvasGroup);
        PrepareHiddenPanel(makersPanelCanvasGroup);
        PrepareHiddenPanel(quitPanelCanvasGroup);

        if (backgroundDimCanvasGroup != null)
        {
            backgroundDimCanvasGroup.alpha = 0f;
            backgroundDimCanvasGroup.interactable = false;
            backgroundDimCanvasGroup.blocksRaycasts = false;
            backgroundDimCanvasGroup.gameObject.SetActive(false);
        }

        HideMakerRowsInstant();
    }

    private void SetupButtonListeners()
    {
        if (settingButton != null)
            settingButton.onClick.AddListener(OpenSettings);

        if (makersButton != null)
            makersButton.onClick.AddListener(OpenMakers);

        if (quitButton != null)
            quitButton.onClick.AddListener(OpenQuitConfirm);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(CloseCurrentPanel);

        if (makersBackButton != null)
            makersBackButton.onClick.AddListener(CloseCurrentPanel);

        if (quitCancelButton != null)
            quitCancelButton.onClick.AddListener(CloseCurrentPanel);

        if (quitConfirmButton != null)
            quitConfirmButton.onClick.AddListener(ConfirmQuit);

        if (resetSettingsButton != null)
            resetSettingsButton.onClick.AddListener(ResetSettingsToDefault);
    }

    private void RemoveButtonListeners()
    {
        if (settingButton != null)
            settingButton.onClick.RemoveListener(OpenSettings);

        if (makersButton != null)
            makersButton.onClick.RemoveListener(OpenMakers);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(OpenQuitConfirm);

        if (settingsBackButton != null)
            settingsBackButton.onClick.RemoveListener(CloseCurrentPanel);

        if (makersBackButton != null)
            makersBackButton.onClick.RemoveListener(CloseCurrentPanel);

        if (quitCancelButton != null)
            quitCancelButton.onClick.RemoveListener(CloseCurrentPanel);

        if (quitConfirmButton != null)
            quitConfirmButton.onClick.RemoveListener(ConfirmQuit);

        if (resetSettingsButton != null)
            resetSettingsButton.onClick.RemoveListener(ResetSettingsToDefault);
    }

    private void SetupSettingListeners()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(
                SetResolutionFromDropdown
            );
        }

        if (resolutionPreviousButton != null)
        {
            resolutionPreviousButton.onClick.AddListener(
                PreviousResolution
            );
        }

        if (resolutionNextButton != null)
        {
            resolutionNextButton.onClick.AddListener(
                NextResolution
            );
        }
    }

    private void RemoveSettingListeners()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.RemoveListener(SetBgmVolume);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(
                SetResolutionFromDropdown
            );
        }

        if (resolutionPreviousButton != null)
        {
            resolutionPreviousButton.onClick.RemoveListener(
                PreviousResolution
            );
        }

        if (resolutionNextButton != null)
        {
            resolutionNextButton.onClick.RemoveListener(
                NextResolution
            );
        }
    }

    public void OpenSettings()
    {
        TryOpenPanel(
            PanelState.Settings,
            settingsPanelCanvasGroup
        );
    }

    public void OpenMakers()
    {
        TryOpenPanel(
            PanelState.Makers,
            makersPanelCanvasGroup
        );
    }

    public void OpenQuitConfirm()
    {
        TryOpenPanel(
            PanelState.QuitConfirm,
            quitPanelCanvasGroup
        );
    }

    public void CloseCurrentPanel()
    {
        if (isTransitioning ||
            currentPanel == PanelState.None)
        {
            return;
        }

        CanvasGroup panel =
            GetCurrentPanelCanvasGroup();

        if (panel == null)
        {
            RestoreMenuInstant();
            return;
        }

        StartTransition(
            ClosePanelRoutine(panel)
        );
    }

    public void ConfirmQuit()
    {
        if (isTransitioning)
            return;

        SaveSettings();

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine =
            StartCoroutine(QuitRoutine());
    }

    private void TryOpenPanel(
        PanelState targetState,
        CanvasGroup targetPanel)
    {
        if (isTransitioning ||
            currentPanel != PanelState.None ||
            targetPanel == null)
        {
            return;
        }

        currentPanel = targetState;

        StartTransition(
            OpenPanelRoutine(
                targetPanel,
                targetState
            )
        );
    }

    private void StartTransition(
        IEnumerator routine)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine =
            StartCoroutine(routine);
    }

    private IEnumerator OpenPanelRoutine(
        CanvasGroup panel,
        PanelState panelState)
    {
        isTransitioning = true;
        SetMainMenuInteraction(false);

        if (menuGroupCanvasGroup != null)
        {
            yield return FadeCanvasGroup(
                menuGroupCanvasGroup,
                menuGroupCanvasGroup.alpha,
                0f,
                menuFadeDuration
            );

            menuGroupCanvasGroup.interactable = false;
            menuGroupCanvasGroup.blocksRaycasts = false;
        }

        ActivateDim();

        panel.gameObject.SetActive(true);
        panel.alpha = 1f;
        panel.interactable = false;
        panel.blocksRaycasts = false;

        MainMenuPanelConstructAnimator constructor =
            panel.GetComponent
            <MainMenuPanelConstructAnimator>();

        Coroutine dimRoutine =
            StartCoroutine(
                FadeDimRoutine(
                    0f,
                    backgroundDimAlpha,
                    constructor != null
                        ? constructor.EstimatedOpenDuration
                        : fallbackPanelFadeDuration
                )
            );

        if (constructor != null)
        {
            yield return constructor.PlayOpen();
        }
        else
        {
            yield return FallbackOpenPanel(panel);
        }

        if (dimRoutine != null)
            yield return dimRoutine;

        panel.alpha = 1f;
        panel.interactable = true;
        panel.blocksRaycasts = true;

        if (backgroundDimCanvasGroup != null)
        {
            backgroundDimCanvasGroup.alpha =
                backgroundDimAlpha;

            backgroundDimCanvasGroup.interactable =
                true;

            backgroundDimCanvasGroup.blocksRaycasts =
                true;
        }

        isTransitioning = false;
        transitionCoroutine = null;

        if (panelState == PanelState.Makers &&
            constructor == null)
        {
            if (makersRevealCoroutine != null)
                StopCoroutine(makersRevealCoroutine);

            makersRevealCoroutine =
                StartCoroutine(
                    RevealMakersRowsRoutine()
                );
        }
    }

    private IEnumerator ClosePanelRoutine(
        CanvasGroup panel)
    {
        isTransitioning = true;

        if (makersRevealCoroutine != null)
        {
            StopCoroutine(makersRevealCoroutine);
            makersRevealCoroutine = null;
        }

        panel.interactable = false;
        panel.blocksRaycasts = false;

        MainMenuPanelConstructAnimator constructor =
            panel.GetComponent
            <MainMenuPanelConstructAnimator>();

        float closeDuration = constructor != null
            ? constructor.EstimatedCloseDuration
            : fallbackPanelFadeDuration;

        Coroutine dimRoutine =
            StartCoroutine(
                FadeDimRoutine(
                    backgroundDimCanvasGroup != null
                        ? backgroundDimCanvasGroup.alpha
                        : backgroundDimAlpha,
                    0f,
                    closeDuration
                )
            );

        if (constructor != null)
        {
            yield return constructor.PlayClose();
        }
        else
        {
            yield return FallbackClosePanel(panel);
        }

        if (dimRoutine != null)
            yield return dimRoutine;

        panel.alpha = 0f;
        panel.gameObject.SetActive(false);

        DeactivateDim();

        currentPanel = PanelState.None;

        if (menuGroupCanvasGroup != null)
        {
            yield return FadeCanvasGroup(
                menuGroupCanvasGroup,
                0f,
                1f,
                menuFadeDuration
            );
        }

        SetMainMenuInteraction(true);

        isTransitioning = false;
        transitionCoroutine = null;
    }

    private IEnumerator FallbackOpenPanel(
        CanvasGroup panel)
    {
        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        Vector3 endScale = Vector3.one;

        Vector3 startScale =
            new Vector3(
                fallbackPanelStartScale,
                fallbackPanelStartScale,
                1f
            );

        if (panelRect != null)
            panelRect.localScale = startScale;

        float elapsed = 0f;
        float duration = Mathf.Max(
            0.01f,
            fallbackPanelFadeDuration
        );

        panel.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            float smoothT =
                1f - Mathf.Pow(1f - t, 3f);

            panel.alpha = smoothT;

            if (panelRect != null)
            {
                panelRect.localScale =
                    Vector3.Lerp(
                        startScale,
                        endScale,
                        smoothT
                    );
            }

            yield return null;
        }

        panel.alpha = 1f;

        if (panelRect != null)
            panelRect.localScale = Vector3.one;
    }

    private IEnumerator FallbackClosePanel(
        CanvasGroup panel)
    {
        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        Vector3 startScale =
            panelRect != null
                ? panelRect.localScale
                : Vector3.one;

        Vector3 endScale =
            new Vector3(
                fallbackPanelStartScale,
                fallbackPanelStartScale,
                1f
            );

        float elapsed = 0f;
        float duration = Mathf.Max(
            0.01f,
            fallbackPanelFadeDuration
        );

        float startAlpha = panel.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            panel.alpha = Mathf.Lerp(
                startAlpha,
                0f,
                t
            );

            if (panelRect != null)
            {
                panelRect.localScale =
                    Vector3.Lerp(
                        startScale,
                        endScale,
                        t
                    );
            }

            yield return null;
        }

        panel.alpha = 0f;

        if (panelRect != null)
            panelRect.localScale = Vector3.one;
    }

    private IEnumerator QuitRoutine()
    {
        isTransitioning = true;

        CanvasGroup panel =
            GetCurrentPanelCanvasGroup();

        if (panel != null)
        {
            panel.interactable = false;
            panel.blocksRaycasts = false;
        }

        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            if (panel != null)
            {
                panel.alpha =
                    Mathf.Lerp(1f, 0f, t);
            }

            if (backgroundDimCanvasGroup != null)
            {
                backgroundDimCanvasGroup.alpha =
                    Mathf.Lerp(
                        backgroundDimAlpha,
                        1f,
                        t
                    );
            }

            yield return null;
        }

#if UNITY_EDITOR
        // Unity Editor에서는 Application.Quit()이 Editor 자체를 종료하지 않으므로
        // Play Mode를 종료해서 실제 QUIT 동작을 테스트할 수 있게 합니다.
        EditorApplication.isPlaying = false;
#else
        // 실제 Windows/Mac/Linux 빌드에서는 게임 프로세스를 종료합니다.
        Application.Quit();
#endif

        isTransitioning = false;
        transitionCoroutine = null;
    }

    private IEnumerator RevealMakersRowsRoutine()
    {
        HideMakerRowsInstant();

        if (makerRows == null)
            yield break;

        for (int i = 0; i < makerRows.Length; i++)
        {
            CanvasGroup row = makerRows[i];

            if (row == null)
                continue;

            row.gameObject.SetActive(true);

            float elapsed = 0f;
            float duration = Mathf.Max(
                0.01f,
                makerRowFadeDuration
            );

            while (elapsed < duration)
            {
                if (currentPanel !=
                    PanelState.Makers)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;

                row.alpha = Mathf.Clamp01(
                    elapsed / duration
                );

                yield return null;
            }

            row.alpha = 1f;

            if (makerRowDelay > 0f)
            {
                yield return
                    new WaitForSecondsRealtime(
                        makerRowDelay
                    );
            }
        }

        makersRevealCoroutine = null;
    }

    private void HideMakerRowsInstant()
    {
        if (makerRows == null)
            return;

        for (int i = 0; i < makerRows.Length; i++)
        {
            if (makerRows[i] != null)
                makerRows[i].alpha = 0f;
        }
    }

    private void ActivateDim()
    {
        if (backgroundDimCanvasGroup == null)
            return;

        backgroundDimCanvasGroup.gameObject.SetActive(
            true
        );

        backgroundDimCanvasGroup.alpha = 0f;
        backgroundDimCanvasGroup.interactable = true;
        backgroundDimCanvasGroup.blocksRaycasts = true;
    }

    private void DeactivateDim()
    {
        if (backgroundDimCanvasGroup == null)
            return;

        backgroundDimCanvasGroup.alpha = 0f;
        backgroundDimCanvasGroup.interactable = false;
        backgroundDimCanvasGroup.blocksRaycasts = false;
        backgroundDimCanvasGroup.gameObject.SetActive(
            false
        );
    }

    private IEnumerator FadeDimRoutine(
        float from,
        float to,
        float duration)
    {
        if (backgroundDimCanvasGroup == null)
            yield break;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(
            0.01f,
            duration
        );

        backgroundDimCanvasGroup.alpha = from;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / safeDuration
            );

            backgroundDimCanvasGroup.alpha =
                Mathf.Lerp(from, to, t);

            yield return null;
        }

        backgroundDimCanvasGroup.alpha = to;
    }

    private CanvasGroup GetCurrentPanelCanvasGroup()
    {
        switch (currentPanel)
        {
            case PanelState.Settings:
                return settingsPanelCanvasGroup;

            case PanelState.Makers:
                return makersPanelCanvasGroup;

            case PanelState.QuitConfirm:
                return quitPanelCanvasGroup;

            default:
                return null;
        }
    }

    private IEnumerator FadeCanvasGroup(
        CanvasGroup group,
        float from,
        float to,
        float duration)
    {
        if (group == null)
            yield break;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(
            0.01f,
            duration
        );

        group.alpha = from;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / safeDuration
            );

            group.alpha =
                Mathf.Lerp(from, to, t);

            yield return null;
        }

        group.alpha = to;
    }

    private void SetMainMenuInteraction(
        bool enabled)
    {
        if (menuGroupCanvasGroup != null)
        {
            menuGroupCanvasGroup.interactable =
                enabled;

            menuGroupCanvasGroup.blocksRaycasts =
                enabled;
        }

        if (mainMenuHoverScripts == null)
            return;

        for (int i = 0;
             i < mainMenuHoverScripts.Length;
             i++)
        {
            if (mainMenuHoverScripts[i] != null)
            {
                mainMenuHoverScripts[i]
                    .SetHoverEnabled(enabled);
            }
        }
    }

    private void RestoreMenuInstant()
    {
        currentPanel = PanelState.None;
        isTransitioning = false;

        DeactivateDim();

        SetCanvasGroupInstant(
            settingsPanelCanvasGroup,
            0f,
            false,
            false
        );

        SetCanvasGroupInstant(
            makersPanelCanvasGroup,
            0f,
            false,
            false
        );

        SetCanvasGroupInstant(
            quitPanelCanvasGroup,
            0f,
            false,
            false
        );

        if (settingsPanelCanvasGroup != null)
            settingsPanelCanvasGroup.gameObject.SetActive(false);

        if (makersPanelCanvasGroup != null)
            makersPanelCanvasGroup.gameObject.SetActive(false);

        if (quitPanelCanvasGroup != null)
            quitPanelCanvasGroup.gameObject.SetActive(false);

        SetCanvasGroupInstant(
            menuGroupCanvasGroup,
            1f,
            true,
            true
        );

        SetMainMenuInteraction(true);
    }

    private void PrepareHiddenPanel(
        CanvasGroup panel)
    {
        if (panel == null)
            return;

        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        panel.gameObject.SetActive(false);
    }

    private void SetCanvasGroupInstant(
        CanvasGroup group,
        float alpha,
        bool interactable,
        bool blocksRaycasts)
    {
        if (group == null)
            return;

        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = blocksRaycasts;
    }

    // -------------------------
    // Settings
    // -------------------------

    private void BuildResolutionList()
    {
        availableResolutions.Clear();

        Resolution[] resolutions =
            Screen.resolutions;

        for (int i = 0;
             i < resolutions.Length;
             i++)
        {
            Resolution resolution =
                resolutions[i];

            bool duplicate = false;

            for (int j = 0;
                 j < availableResolutions.Count;
                 j++)
            {
                if (availableResolutions[j].width ==
                        resolution.width &&
                    availableResolutions[j].height ==
                        resolution.height)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                availableResolutions.Add(resolution);
        }

        if (availableResolutions.Count == 0)
            availableResolutions.Add(
                Screen.currentResolution
            );

        availableResolutions.Sort(
            (a, b) =>
            {
                int widthCompare =
                    a.width.CompareTo(b.width);

                return widthCompare != 0
                    ? widthCompare
                    : a.height.CompareTo(b.height);
            }
        );
    }

    private void PopulateLegacyResolutionDropdown()
    {
        if (resolutionDropdown == null)
            return;

        List<string> options =
            new List<string>();

        for (int i = 0;
             i < availableResolutions.Count;
             i++)
        {
            options.Add(
                availableResolutions[i].width +
                " x " +
                availableResolutions[i].height
            );
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
    }

    private void LoadAndApplySettings()
    {
        float master = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                MasterVolumeKey,
                defaultMasterVolume
            )
        );

        float bgm = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                BgmVolumeKey,
                defaultBgmVolume
            )
        );

        float sfx = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                SfxVolumeKey,
                defaultSfxVolume
            )
        );

        bool fullscreen =
            PlayerPrefs.GetInt(
                FullscreenKey,
                defaultFullscreen ? 1 : 0
            ) == 1;

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(master);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.SetValueWithoutNotify(bgm);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(sfx);

        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

        ApplyMasterVolume(master);
        ApplyBgmVolume(bgm);
        ApplySfxVolume(sfx);

        int savedWidth =
            PlayerPrefs.GetInt(
                ResolutionWidthKey,
                Screen.width
            );

        int savedHeight =
            PlayerPrefs.GetInt(
                ResolutionHeightKey,
                Screen.height
            );

        currentResolutionIndex =
            FindResolutionIndex(
                savedWidth,
                savedHeight
            );

        if (currentResolutionIndex < 0)
        {
            currentResolutionIndex =
                FindResolutionIndex(
                    Screen.width,
                    Screen.height
                );
        }

        if (currentResolutionIndex < 0)
        {
            currentResolutionIndex =
                Mathf.Max(
                    0,
                    availableResolutions.Count - 1
                );
        }

        UpdateVolumeTexts(
            master,
            bgm,
            sfx
        );

        UpdateResolutionUI();
    }

    private void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(
            MasterVolumeKey,
            value
        );

        ApplyMasterVolume(value);

        if (masterVolumeValueText != null)
        {
            masterVolumeValueText.text =
                Mathf.RoundToInt(
                    value * 100f
                ) + "%";
        }

        PlayerPrefs.Save();
    }

    private void SetBgmVolume(float value)
    {
        value = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(
            BgmVolumeKey,
            value
        );

        ApplyBgmVolume(value);

        if (bgmVolumeValueText != null)
        {
            bgmVolumeValueText.text =
                Mathf.RoundToInt(
                    value * 100f
                ) + "%";
        }

        PlayerPrefs.Save();
    }

    private void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(
            SfxVolumeKey,
            value
        );

        ApplySfxVolume(value);

        if (sfxVolumeValueText != null)
        {
            sfxVolumeValueText.text =
                Mathf.RoundToInt(
                    value * 100f
                ) + "%";
        }

        PlayerPrefs.Save();
    }

    private void ApplyMasterVolume(float value)
    {
        GlobalAudioSettingsV14.SetMaster(Mathf.Clamp01(value));
    }

    private void ApplyBgmVolume(float value)
    {
        // V14: main-menu and in-game BGM now share the same runtime setting.
        GlobalAudioSettingsV14.SetBgm(Mathf.Clamp01(value));
    }

    private void ApplySfxVolume(float value)
    {
        // V14: source-level SFX scaling is global, so the legacy menu-only multiplier stays neutral.
        MenuTextButtonHover.SetGlobalSfxVolume(1f);
        GlobalAudioSettingsV14.SetSfx(Mathf.Clamp01(value));
    }

    private void UpdateVolumeTexts(
        float master,
        float bgm,
        float sfx)
    {
        if (masterVolumeValueText != null)
        {
            masterVolumeValueText.text =
                Mathf.RoundToInt(
                    master * 100f
                ) + "%";
        }

        if (bgmVolumeValueText != null)
        {
            bgmVolumeValueText.text =
                Mathf.RoundToInt(
                    bgm * 100f
                ) + "%";
        }

        if (sfxVolumeValueText != null)
        {
            sfxVolumeValueText.text =
                Mathf.RoundToInt(
                    sfx * 100f
                ) + "%";
        }
    }

    private void SetFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt(
            FullscreenKey,
            fullscreen ? 1 : 0
        );

        ApplyCurrentResolution(
            fullscreen,
            true
        );
    }

    private void PreviousResolution()
    {
        ChangeResolution(-1);
    }

    private void NextResolution()
    {
        ChangeResolution(1);
    }

    private void ChangeResolution(int direction)
    {
        if (availableResolutions.Count == 0)
            return;

        currentResolutionIndex += direction;

        if (currentResolutionIndex < 0)
        {
            currentResolutionIndex =
                availableResolutions.Count - 1;
        }

        if (currentResolutionIndex >=
            availableResolutions.Count)
        {
            currentResolutionIndex = 0;
        }

        bool fullscreen =
            fullscreenToggle != null
                ? fullscreenToggle.isOn
                : Screen.fullScreen;

        ApplyCurrentResolution(
            fullscreen,
            true
        );
    }

    private void SetResolutionFromDropdown(
        int index)
    {
        if (index < 0 ||
            index >= availableResolutions.Count)
        {
            return;
        }

        currentResolutionIndex = index;

        bool fullscreen =
            fullscreenToggle != null
                ? fullscreenToggle.isOn
                : Screen.fullScreen;

        ApplyCurrentResolution(
            fullscreen,
            true
        );
    }

    private void ApplyCurrentResolution(
        bool fullscreen,
        bool save)
    {
        if (availableResolutions.Count == 0)
            return;

        currentResolutionIndex =
            Mathf.Clamp(
                currentResolutionIndex,
                0,
                availableResolutions.Count - 1
            );

        Resolution selected =
            availableResolutions[
                currentResolutionIndex
            ];

        Screen.SetResolution(
            selected.width,
            selected.height,
            fullscreen
        );

        UpdateResolutionUI();

        if (save)
        {
            PlayerPrefs.SetInt(
                ResolutionWidthKey,
                selected.width
            );

            PlayerPrefs.SetInt(
                ResolutionHeightKey,
                selected.height
            );

            PlayerPrefs.SetInt(
                FullscreenKey,
                fullscreen ? 1 : 0
            );

            PlayerPrefs.Save();
        }
    }

    private void UpdateResolutionUI()
    {
        if (availableResolutions.Count == 0)
            return;

        Resolution selected =
            availableResolutions[
                Mathf.Clamp(
                    currentResolutionIndex,
                    0,
                    availableResolutions.Count - 1
                )
            ];

        if (resolutionValueText != null)
        {
            resolutionValueText.text =
                selected.width +
                " x " +
                selected.height;
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.SetValueWithoutNotify(
                Mathf.Clamp(
                    currentResolutionIndex,
                    0,
                    resolutionDropdown.options.Count - 1
                )
            );
        }
    }

    private int FindResolutionIndex(
        int width,
        int height)
    {
        for (int i = 0;
             i < availableResolutions.Count;
             i++)
        {
            if (availableResolutions[i].width ==
                    width &&
                availableResolutions[i].height ==
                    height)
            {
                return i;
            }
        }

        return -1;
    }

    private void ResetSettingsToDefault()
    {
        float master =
            Mathf.Clamp01(defaultMasterVolume);

        float bgm =
            Mathf.Clamp01(defaultBgmVolume);

        float sfx =
            Mathf.Clamp01(defaultSfxVolume);

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(master);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.SetValueWithoutNotify(bgm);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(sfx);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(
                defaultFullscreen
            );
        }

        SetMasterVolume(master);
        SetBgmVolume(bgm);
        SetSfxVolume(sfx);

        currentResolutionIndex =
            FindResolutionIndex(
                Screen.currentResolution.width,
                Screen.currentResolution.height
            );

        if (currentResolutionIndex < 0)
        {
            currentResolutionIndex =
                Mathf.Max(
                    0,
                    availableResolutions.Count - 1
                );
        }

        ApplyCurrentResolution(
            defaultFullscreen,
            true
        );

        PlayerPrefs.Save();
    }

    private void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    private void OnValidate()
    {
        backgroundDimAlpha =
            Mathf.Clamp01(backgroundDimAlpha);

        menuFadeDuration =
            Mathf.Max(0f, menuFadeDuration);

        fallbackPanelFadeDuration =
            Mathf.Max(
                0f,
                fallbackPanelFadeDuration
            );

        fallbackPanelStartScale =
            Mathf.Clamp(
                fallbackPanelStartScale,
                0.5f,
                1f
            );

        makerRowFadeDuration =
            Mathf.Max(
                0f,
                makerRowFadeDuration
            );

        makerRowDelay =
            Mathf.Max(
                0f,
                makerRowDelay
            );

        defaultMasterVolume =
            Mathf.Clamp01(
                defaultMasterVolume
            );

        defaultBgmVolume =
            Mathf.Clamp01(
                defaultBgmVolume
            );

        defaultSfxVolume =
            Mathf.Clamp01(
                defaultSfxVolume
            );
    }
}
