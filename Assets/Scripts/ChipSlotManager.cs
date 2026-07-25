using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChipSlotManager : MonoBehaviour
{
    public static ChipSlotManager Instance { get; private set; }

    public enum ChipType
    {
        None,

        MeleeDamage,        // 1번
        MeleeAttackSpeed,   // 2번
        MeleeRange,         // 3번

        RangedDamage,       // 4번
        RangedAttackSpeed,  // 5번
        RangedPierce,       // 6번

        Defense,            // 7번
        MaxHealth,          // 8번
        MoveSpeed           // 9번
    }

    [Header("UI Slots")]
    [SerializeField] private Image[] equippedSlotImages;
    [SerializeField] private Sprite emptySlotSprite;
    [SerializeField] private int maxEquippedCount = 3;

    [Header("Slot Animation")]
    [SerializeField] private bool useSlotAnimation = true;
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float popDuration = 0.15f;
    [SerializeField] private float startScale = 0.75f;
    [SerializeField] private float popScale = 1.15f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip equipSound;
    [SerializeField] private AudioClip unequipSound;
    [SerializeField] private AudioClip replaceSound;
    [SerializeField] private float soundVolume = 0.55f;
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.06f);

    [Header("Chip Sprites")]
    [SerializeField] private Sprite meleeDamageSprite;
    [SerializeField] private Sprite meleeAttackSpeedSprite;
    [SerializeField] private Sprite meleeRangeSprite;

    [SerializeField] private Sprite rangedDamageSprite;
    [SerializeField] private Sprite rangedAttackSpeedSprite;
    [SerializeField] private Sprite rangedPierceSprite;

    [SerializeField] private Sprite defenseSprite;
    [SerializeField] private Sprite maxHealthSprite;
    [SerializeField] private Sprite moveSpeedSprite;

    [Header("Existing Completed Effects")]
    [SerializeField] private GameObject overclockObject;

    [Header("Effect Values")]
    [SerializeField] private float meleeDamageMultiplier = 1.5f;
    [SerializeField] private float meleeAttackSpeedMultiplier = 1.3f;
    [SerializeField] private float meleeRangeMultiplier = 1.4f;

    [SerializeField] private float rangedDamageMultiplier = 1.5f;
    [SerializeField] private float rangedAttackSpeedMultiplier = 1.3f;
    [SerializeField] private bool rangedPierceEnabled = true;

    [SerializeField] private float defenseDamageMultiplier = 0.7f;
    [SerializeField] private float moveSpeedMultiplier = 1.3f;

    [Header("Input Option")]
    [SerializeField] private bool allowTopNumberKeys = false;

    private readonly List<ChipType> equippedChips = new List<ChipType>();

    private CanvasGroup[] slotCanvasGroups;
    private Vector3[] slotOriginalScales;
    private Coroutine[] slotAnimationCoroutines;
    private Sprite[] currentSlotSprites;
    private bool[] currentSlotVisible;

    public float MeleeDamageMultiplier => IsChipEquipped(ChipType.MeleeDamage) ? meleeDamageMultiplier : 1f;
    public float MeleeAttackSpeedMultiplier => IsChipEquipped(ChipType.MeleeAttackSpeed) ? meleeAttackSpeedMultiplier : 1f;
    public float MeleeRangeMultiplier => IsChipEquipped(ChipType.MeleeRange) ? meleeRangeMultiplier : 1f;

    public float RangedDamageMultiplier => IsChipEquipped(ChipType.RangedDamage) ? rangedDamageMultiplier : 1f;
    public float RangedAttackSpeedMultiplier => IsChipEquipped(ChipType.RangedAttackSpeed) ? rangedAttackSpeedMultiplier : 1f;
    public bool IsRangedPierceEnabled => IsChipEquipped(ChipType.RangedPierce) && rangedPierceEnabled;

    public float DefenseDamageMultiplier => IsChipEquipped(ChipType.Defense) ? defenseDamageMultiplier : 1f;
    public float MoveSpeedMultiplier => IsChipEquipped(ChipType.MoveSpeed) ? moveSpeedMultiplier : 1f;

    private void Awake()
    {
        Instance = this;

        if (maxEquippedCount <= 0)
        {
            maxEquippedCount = 3;
        }

        SetupAudioSource();
        SetupSlotAnimationData();
    }

    private void Start()
    {
        RefreshSlotUI(true);
    }

    private void Update()
    {
        if (GetNumberKeyDown(KeyCode.Keypad1, KeyCode.Alpha1))
        {
            ToggleChip(ChipType.MeleeDamage);
        }

        if (GetNumberKeyDown(KeyCode.Keypad2, KeyCode.Alpha2))
        {
            ToggleChip(ChipType.MeleeAttackSpeed);
        }

        if (GetNumberKeyDown(KeyCode.Keypad3, KeyCode.Alpha3))
        {
            ToggleChip(ChipType.MeleeRange);
        }

        if (GetNumberKeyDown(KeyCode.Keypad4, KeyCode.Alpha4))
        {
            ToggleChip(ChipType.RangedDamage);
        }

        if (GetNumberKeyDown(KeyCode.Keypad5, KeyCode.Alpha5))
        {
            ToggleChip(ChipType.RangedAttackSpeed);
        }

        if (GetNumberKeyDown(KeyCode.Keypad6, KeyCode.Alpha6))
        {
            ToggleChip(ChipType.RangedPierce);
        }

        if (GetNumberKeyDown(KeyCode.Keypad7, KeyCode.Alpha7))
        {
            ToggleChip(ChipType.Defense);
        }

        if (GetNumberKeyDown(KeyCode.Keypad8, KeyCode.Alpha8))
        {
            ToggleChip(ChipType.MaxHealth);
        }

        if (GetNumberKeyDown(KeyCode.Keypad9, KeyCode.Alpha9))
        {
            ToggleChip(ChipType.MoveSpeed);
        }
    }

    private bool GetNumberKeyDown(KeyCode keypadKey, KeyCode topNumberKey)
    {
        if (Input.GetKeyDown(keypadKey))
        {
            return true;
        }

        if (allowTopNumberKeys && Input.GetKeyDown(topNumberKey))
        {
            return true;
        }

        return false;
    }

    public void ToggleChip(ChipType chipType)
    {
        if (chipType == ChipType.None)
        {
            return;
        }

        if (IsChipEquipped(chipType))
        {
            UnequipChip(chipType);
        }
        else
        {
            EquipChip(chipType);
        }
    }

    public void EquipChip(ChipType chipType)
    {
        if (chipType == ChipType.None)
        {
            return;
        }

        if (IsChipEquipped(chipType))
        {
            return;
        }

        bool replacedOldChip = false;

        while (equippedChips.Count >= maxEquippedCount)
        {
            replacedOldChip = true;

            ChipType oldestChip = equippedChips[0];
            RemoveChipEffect(oldestChip);
            equippedChips.RemoveAt(0);
        }

        equippedChips.Add(chipType);
        ApplyChipEffect(chipType);

        RefreshSlotUI(false);

        if (replacedOldChip)
        {
            PlaySound(replaceSound);
        }
        else
        {
            PlaySound(equipSound);
        }

        Debug.Log("Chip Equipped: " + chipType);
    }

    public void UnequipChip(ChipType chipType)
    {
        if (!IsChipEquipped(chipType))
        {
            return;
        }

        RemoveChipEffect(chipType);
        equippedChips.Remove(chipType);

        RefreshSlotUI(false);
        PlaySound(unequipSound);

        Debug.Log("Chip Unequipped: " + chipType);
    }

    public bool IsChipEquipped(ChipType chipType)
    {
        return equippedChips.Contains(chipType);
    }

    private void ApplyChipEffect(ChipType chipType)
    {
        switch (chipType)
        {
            case ChipType.MaxHealth:
                EnableOverclockEffect();
                break;

            case ChipType.MeleeDamage:
                break;

            case ChipType.MeleeAttackSpeed:
                break;

            case ChipType.MeleeRange:
                break;

            case ChipType.RangedDamage:
                break;

            case ChipType.RangedAttackSpeed:
                break;

            case ChipType.RangedPierce:
                break;

            case ChipType.Defense:
                break;

            case ChipType.MoveSpeed:
                break;
        }
    }

    private void RemoveChipEffect(ChipType chipType)
    {
        switch (chipType)
        {
            case ChipType.MaxHealth:
                DisableOverclockEffect();
                break;

            case ChipType.MeleeDamage:
                break;

            case ChipType.MeleeAttackSpeed:
                break;

            case ChipType.MeleeRange:
                break;

            case ChipType.RangedDamage:
                break;

            case ChipType.RangedAttackSpeed:
                break;

            case ChipType.RangedPierce:
                break;

            case ChipType.Defense:
                break;

            case ChipType.MoveSpeed:
                break;
        }
    }

    private void EnableOverclockEffect()
    {
        if (overclockObject == null)
        {
            Debug.LogWarning("ChipSlotManager: Overclock Object가 연결되지 않았습니다.");
            return;
        }

        overclockObject.SendMessage("EnableOverclock", SendMessageOptions.DontRequireReceiver);
    }

    private void DisableOverclockEffect()
    {
        if (overclockObject == null)
        {
            return;
        }

        overclockObject.SendMessage("DisableOverclock", SendMessageOptions.DontRequireReceiver);
    }

    private void RefreshSlotUI(bool instant)
    {
        if (equippedSlotImages == null)
        {
            return;
        }

        SetupSlotAnimationData();

        for (int i = 0; i < equippedSlotImages.Length; i++)
        {
            Image slotImage = equippedSlotImages[i];

            if (slotImage == null)
            {
                continue;
            }

            Sprite targetSprite = null;
            bool shouldShow = false;

            if (i < equippedChips.Count)
            {
                targetSprite = GetChipSprite(equippedChips[i]);
                shouldShow = targetSprite != null;
            }
            else if (emptySlotSprite != null)
            {
                targetSprite = emptySlotSprite;
                shouldShow = true;
            }

            if (instant || !useSlotAnimation)
            {
                SetSlotInstant(i, targetSprite, shouldShow);
            }
            else
            {
                bool spriteChanged = currentSlotSprites[i] != targetSprite;
                bool visibleChanged = currentSlotVisible[i] != shouldShow;

                if (spriteChanged || visibleChanged)
                {
                    PlaySlotAnimation(i, targetSprite, shouldShow);
                }
            }
        }
    }

    private void SetSlotInstant(int index, Sprite targetSprite, bool shouldShow)
    {
        if (!IsValidSlotIndex(index))
        {
            return;
        }

        Image slotImage = equippedSlotImages[index];
        CanvasGroup canvasGroup = slotCanvasGroups[index];

        if (slotAnimationCoroutines[index] != null)
        {
            StopCoroutine(slotAnimationCoroutines[index]);
            slotAnimationCoroutines[index] = null;
        }

        slotImage.sprite = targetSprite;
        slotImage.enabled = shouldShow;
        slotImage.preserveAspect = true;
        slotImage.gameObject.SetActive(shouldShow);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = shouldShow ? 1f : 0f;
        }

        slotImage.rectTransform.localScale = slotOriginalScales[index];

        currentSlotSprites[index] = targetSprite;
        currentSlotVisible[index] = shouldShow;
    }

    private void PlaySlotAnimation(int index, Sprite targetSprite, bool shouldShow)
    {
        if (!IsValidSlotIndex(index))
        {
            return;
        }

        if (slotAnimationCoroutines[index] != null)
        {
            StopCoroutine(slotAnimationCoroutines[index]);
        }

        slotAnimationCoroutines[index] = StartCoroutine(SlotChangeRoutine(index, targetSprite, shouldShow));
    }

    private IEnumerator SlotChangeRoutine(int index, Sprite targetSprite, bool shouldShow)
    {
        Image slotImage = equippedSlotImages[index];
        CanvasGroup canvasGroup = slotCanvasGroups[index];
        RectTransform rectTransform = slotImage.rectTransform;
        Vector3 originalScale = slotOriginalScales[index];

        if (canvasGroup == null)
        {
            SetSlotInstant(index, targetSprite, shouldShow);
            yield break;
        }

        bool wasVisible = currentSlotVisible[index];

        if (wasVisible)
        {
            float outElapsed = 0f;
            Vector3 outStartScale = rectTransform.localScale;
            Vector3 outEndScale = originalScale * startScale;

            while (outElapsed < fadeDuration)
            {
                outElapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(outElapsed / fadeDuration);
                float eased = EaseInQuad(t);

                canvasGroup.alpha = Mathf.Lerp(1f, 0f, eased);
                rectTransform.localScale = Vector3.Lerp(outStartScale, outEndScale, eased);

                yield return null;
            }
        }

        slotImage.sprite = targetSprite;
        slotImage.enabled = shouldShow;
        slotImage.preserveAspect = true;
        slotImage.gameObject.SetActive(shouldShow);

        currentSlotSprites[index] = targetSprite;
        currentSlotVisible[index] = shouldShow;

        if (!shouldShow)
        {
            canvasGroup.alpha = 0f;
            rectTransform.localScale = originalScale;
            slotAnimationCoroutines[index] = null;
            yield break;
        }

        canvasGroup.alpha = 0f;
        rectTransform.localScale = originalScale * startScale;

        float inElapsed = 0f;
        Vector3 inStartScale = originalScale * startScale;
        Vector3 inPopScale = originalScale * popScale;

        while (inElapsed < popDuration)
        {
            inElapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(inElapsed / popDuration);
            float eased = EaseOutBack(t);

            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            rectTransform.localScale = Vector3.Lerp(inStartScale, inPopScale, eased);

            yield return null;
        }

        float settleElapsed = 0f;
        float settleDuration = 0.08f;
        Vector3 settleStartScale = rectTransform.localScale;

        while (settleElapsed < settleDuration)
        {
            settleElapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(settleElapsed / settleDuration);
            rectTransform.localScale = Vector3.Lerp(settleStartScale, originalScale, t);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        rectTransform.localScale = originalScale;

        slotAnimationCoroutines[index] = null;
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null)
        {
            return;
        }

        if (clip == null)
        {
            return;
        }

        float originalPitch = audioSource.pitch;

        if (randomizePitch)
        {
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        }

        audioSource.PlayOneShot(clip, soundVolume);

        audioSource.pitch = originalPitch;
    }

    private void SetupSlotAnimationData()
    {
        int length = equippedSlotImages != null ? equippedSlotImages.Length : 0;

        if (slotCanvasGroups == null || slotCanvasGroups.Length != length)
        {
            slotCanvasGroups = new CanvasGroup[length];
            slotOriginalScales = new Vector3[length];
            slotAnimationCoroutines = new Coroutine[length];
            currentSlotSprites = new Sprite[length];
            currentSlotVisible = new bool[length];
        }

        for (int i = 0; i < length; i++)
        {
            Image slotImage = equippedSlotImages[i];

            if (slotImage == null)
            {
                continue;
            }

            CanvasGroup canvasGroup = slotImage.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = slotImage.gameObject.AddComponent<CanvasGroup>();
            }

            slotCanvasGroups[i] = canvasGroup;

            if (slotOriginalScales[i] == Vector3.zero)
            {
                slotOriginalScales[i] = slotImage.rectTransform.localScale;
            }
        }
    }

    private bool IsValidSlotIndex(int index)
    {
        if (equippedSlotImages == null)
        {
            return false;
        }

        if (index < 0 || index >= equippedSlotImages.Length)
        {
            return false;
        }

        if (equippedSlotImages[index] == null)
        {
            return false;
        }

        return true;
    }

    private float EaseInQuad(float t)
    {
        return t * t;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private Sprite GetChipSprite(ChipType chipType)
    {
        switch (chipType)
        {
            case ChipType.MeleeDamage:
                return meleeDamageSprite;

            case ChipType.MeleeAttackSpeed:
                return meleeAttackSpeedSprite;

            case ChipType.MeleeRange:
                return meleeRangeSprite;

            case ChipType.RangedDamage:
                return rangedDamageSprite;

            case ChipType.RangedAttackSpeed:
                return rangedAttackSpeedSprite;

            case ChipType.RangedPierce:
                return rangedPierceSprite;

            case ChipType.Defense:
                return defenseSprite;

            case ChipType.MaxHealth:
                return maxHealthSprite;

            case ChipType.MoveSpeed:
                return moveSpeedSprite;

            default:
                return null;
        }
    }
}