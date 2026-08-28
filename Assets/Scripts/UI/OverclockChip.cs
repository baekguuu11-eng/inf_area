using System.Collections;
using UnityEngine;

public class OverclockChip : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public GameObject buffIcon;
    [SerializeField] private GameObject[] extraBuffIcons;
    public KeyCode toggleKey = KeyCode.None;
    public int bonusHealthAmount = 3;
    public float heartDelay = 0.12f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip applySound;
    [SerializeField] private float applyVolume = 0.55f;
    [SerializeField] private bool randomizeApplyPitch = true;
    [SerializeField] private Vector2 applyPitchRange = new Vector2(0.94f, 1.06f);

    private PlayerStats stats;
    private bool isOverclockActive;
    private Coroutine overclockCoroutine;

    public bool IsOverclockActive { get { return isOverclockActive; } }

    private void Awake()
    {
        FindPlayerHealthIfNeeded();
        if (playerHealth != null)
            stats = playerHealth.GetComponent<PlayerStats>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        HideAllBuffIcons();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            ToggleOverclock();
#endif
    }

    public void ToggleOverclock()
    {
        if (isOverclockActive) DisableOverclock();
        else EnableOverclock();
    }

    public void EnableOverclock()
    {
        if (isOverclockActive)
            return;

        if (overclockCoroutine != null)
            StopCoroutine(overclockCoroutine);
        overclockCoroutine = StartCoroutine(EnableOverclockRoutine());
    }

    public void DisableOverclock()
    {
        if (overclockCoroutine != null)
        {
            StopCoroutine(overclockCoroutine);
            overclockCoroutine = null;
        }

        isOverclockActive = false;
        if (stats != null)
            stats.SetOverclockHealthBonus(0);
        else if (playerHealth != null)
            playerHealth.RemoveOverclockHealth();

        HideAllBuffIcons();
    }

    private IEnumerator EnableOverclockRoutine()
    {
        FindPlayerHealthIfNeeded();
        if (playerHealth != null && stats == null)
            stats = playerHealth.GetComponent<PlayerStats>();

        isOverclockActive = true;

        if (stats != null)
            stats.SetOverclockHealthBonus(Mathf.Max(0, bonusHealthAmount));
        else if (playerHealth != null)
            playerHealth.ApplyOverclockHealth(Mathf.Max(0, bonusHealthAmount));

        PlayApplySound();
        ShowAllBuffIcons();

        if (playerHealth != null && playerHealth.heartUI != null)
        {
            playerHealth.heartUI.PrepareBonusHeartAnimation(Mathf.Max(0, bonusHealthAmount));
            for (int i = 0; i < Mathf.Max(0, bonusHealthAmount); i++)
            {
                playerHealth.heartUI.ShowBonusHeart(i);
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, heartDelay));
            }
            playerHealth.RefreshUI();
        }

        overclockCoroutine = null;
    }

    private void ShowAllBuffIcons()
    {
        if (buffIcon != null) buffIcon.SetActive(true);
        if (extraBuffIcons == null) return;
        for (int i = 0; i < extraBuffIcons.Length; i++)
            if (extraBuffIcons[i] != null) extraBuffIcons[i].SetActive(true);
    }

    private void HideAllBuffIcons()
    {
        if (buffIcon != null) buffIcon.SetActive(false);
        if (extraBuffIcons == null) return;
        for (int i = 0; i < extraBuffIcons.Length; i++)
            if (extraBuffIcons[i] != null) extraBuffIcons[i].SetActive(false);
    }

    private void PlayApplySound()
    {
        if (audioSource == null || applySound == null)
            return;

        float oldPitch = audioSource.pitch;
        if (randomizeApplyPitch)
            audioSource.pitch = Random.Range(applyPitchRange.x, applyPitchRange.y);
        audioSource.PlayOneShot(applySound, applyVolume);
        audioSource.pitch = oldPitch;
    }

    private void FindPlayerHealthIfNeeded()
    {
        if (playerHealth != null)
            return;
        playerHealth = FindAnyObjectByType<PlayerHealth>();
    }
}
