using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class ChipSlotManager : MonoBehaviour
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
        MoveSpeed,
        RoomRepair, AmmoRecovery, DataCollector, AmmoEfficiency, DeathBurst, SlowRounds
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
    [SerializeField] private float meleeDamageMultiplier = 1.20f;
    [SerializeField] private float meleeAttackSpeedMultiplier = 1.15f;
    [SerializeField] private float meleeRangeMultiplier = 1.15f;

    [SerializeField] private float rangedDamageMultiplier = 1.18f;
    [SerializeField] private float rangedAttackSpeedMultiplier = 1.12f;
    [SerializeField] private bool rangedPierceEnabled = true;

    [SerializeField] private float defenseDamageMultiplier = 0.82f;
    [SerializeField] private float moveSpeedMultiplier = 1.12f;

    [Header("Input Option")]
    [SerializeField] private bool allowTopNumberKeys = true;

    private readonly List<ChipType> equippedChips = new List<ChipType>();
    private readonly HashSet<ChipType> ownedChips = new HashSet<ChipType>();

    public IReadOnlyCollection<ChipType> OwnedChips => ownedChips;

    private CanvasGroup[] slotCanvasGroups;
    private Vector3[] slotOriginalScales;
    private Coroutine[] slotAnimationCoroutines;
    private Sprite[] currentSlotSprites;
    private bool[] currentSlotVisible;

    public float MeleeDamageMultiplier => IsChipEquipped(ChipType.MeleeDamage)?1.20f:1f;
    public float MeleeAttackSpeedMultiplier => 1f;
    public float MeleeRangeMultiplier => 1f;
    public float RangedDamageMultiplier => 1f;
    public float RangedAttackSpeedMultiplier => 1f;
    public bool IsRangedPierceEnabled => false;
    public float DefenseDamageMultiplier => 1f;
    public float MoveSpeedMultiplier => IsChipEquipped(ChipType.MoveSpeed)?1.12f:1f;
    public int MaxHealthBonus => IsChipEquipped(ChipType.MaxHealth)?1:0;
    public static string GetChipDisplayName(ChipType type) => ChipCatalogV22.Name(type);
    public static string GetChipEffectText(ChipType type) => ChipCatalogV22.Description(type);
    public IReadOnlyList<ChipType> Equipped => equippedChips;
    public bool SlotsFull => equippedChips.Count>=maxEquippedCount;
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
        MapManager map=FindAnyObjectByType<MapManager>();if(map!=null) VisitRoomV22(map.CurrentRoom);
    }

    private void Update()
    {
        if(GameInputState.IsLocked || ChipPanelV22.CapturesInput) return;
        PlayerHealth hp=FindAnyObjectByType<PlayerHealth>();if(hp==null || hp.IsDead) return;
        if(Input.GetKeyDown(KeyCode.C)) { ChipPanelV22.OpenInventory(this); return; }
        if(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return;
        for(int i=0;i<ChipCatalogV22.Types.Length;i++)
            if(GetNumberKeyDown((KeyCode)((int)KeyCode.Keypad1+i),(KeyCode)((int)KeyCode.Alpha1+i))) ToggleChip(ChipCatalogV22.Types[i]);
    }
    private void OnDestroy() { if(Instance==this) Instance=null; }
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

        // Chips must come from the shop first. Number keys 1-9 only manage chips the
        // player actually owns; they no longer grant free chip effects.
        if (!OwnsChip(chipType))
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

    public bool OwnsChip(ChipType chipType)
    {
        return chipType != ChipType.None && ownedChips.Contains(chipType);
    }

    /// <summary>
    /// Called by the shop. Buying a chip permanently unlocks it for this run and
    /// immediately equips it so the purchase is visible in the three active chip slots.
    /// The player can then toggle/re-equip owned chips with number keys 1-9.
    /// </summary>
    public bool PurchaseChip(ChipType chipType)
    {
        if (!ChipCatalogV22.Contains(chipType) || ownedChips.Contains(chipType))
            return false;

        ownedChips.Add(chipType);
        EquipChip(chipType);
        Debug.Log("Chip Purchased: " + chipType);
        return true;
    }

    public int GetChipHotkeyNumber(ChipType type) { return System.Array.IndexOf(ChipCatalogV22.Types,type)+1; }
    public void PurchaseChipToSlot(ChipType type,int index)
    {
        if(!ChipCatalogV22.Contains(type)) return;
        ownedChips.Add(type); EquipIntoSlot(type,index);
    }
    public void EquipIntoSlot(ChipType type,int index)
    {
        if(!OwnsChip(type) || IsChipEquipped(type)) return;
        if(SlotsFull)
        {
            if(index<0 || index>=equippedChips.Count) return;
            RemoveChipEffect(equippedChips[index]); equippedChips[index]=type;
        }
        else equippedChips.Add(type);
        ApplyChipEffect(type); RefreshSlotUI(false); PlaySound(equipSound);
    }
    public void EquipChip(ChipType chipType)
    {
        if (chipType == ChipType.None || !OwnsChip(chipType))
        {
            return;
        }

        if (IsChipEquipped(chipType))
        {
            return;
        }

        if(SlotsFull) { ChipPanelV22.OpenReplacement(this,chipType,index=>EquipIntoSlot(chipType,index)); return; }
        EquipIntoSlot(chipType,-1);
    }

    public void UnequipChip(ChipType chipType)
    {
        if (!IsChipEquipped(chipType))
        {
            return;
        }

        RemoveChipEffect(chipType);
        equippedChips.Remove(chipType);
        RefreshPlayerStats();

        RefreshSlotUI(false);
        PlaySound(unequipSound);

        Debug.Log("Chip Unequipped: " + chipType);
    }

    public bool IsChipEquipped(ChipType chipType)
    {
        return equippedChips.Contains(chipType);
    }

    private void ApplyChipEffect(ChipType type) { RefreshPlayerStats(); }
    private void RemoveChipEffect(ChipType type) { }
    private void RefreshPlayerStats()
    {
        PlayerStats stats=FindAnyObjectByType<PlayerStats>(); if(stats!=null) stats.ForceRefresh();
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

    public Sprite GetChipSprite(ChipType type) { return ChipCatalogV22.Icon(type); }
}
