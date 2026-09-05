using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public sealed class AmmoPickup : MonoBehaviour
{
    private static Sprite fallbackSprite;
    private int amount;
    private RoomController ownerRoom;
    private bool collected;
    private Coroutine routine;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer glowRenderer;
    private float glowClock;

    public bool IsCollected => collected;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer.sprite == null) spriteRenderer.sprite = GetFallbackSprite();
        spriteRenderer.sortingOrder = 14;
        GameObject glow = new GameObject("AmmoEnergyGlow");
        glow.transform.SetParent(transform, false);
        glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = RuntimePixelSpriteFactory.GetEnergySparkSprite();
        glowRenderer.sortingOrder = 13;
        glowRenderer.color = new Color(0.18f, 0.90f, 1f, 0.16f);
        V620SpriteMaterialUtility.Apply(glowRenderer);
        glow.transform.localScale = Vector3.one * 0.34f;
    }

    private void Update()
    {
        if (collected || glowRenderer == null) return;
        glowClock += Time.unscaledDeltaTime;
        float pulse = Mathf.Sin(glowClock * 4.8f) * 0.5f + 0.5f;
        Color c = glowRenderer.color;
        c.a = Mathf.Lerp(0.08f, 0.22f, pulse);
        glowRenderer.color = c;
        glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.28f, 0.40f, pulse);
    }

    public void Initialize(int ammoAmount, RoomController room)
    {
        amount = Mathf.Max(1, ammoAmount);
        ownerRoom = room != null ? room : GetComponentInParent<RoomController>();
        if (ownerRoom != null)
        {
            transform.SetParent(ownerRoom.transform, true);
            AmmoRoomRegistry.Register(this, ownerRoom);
        }
        routine = StartCoroutine(ScatterAndWait());
    }

    public void ForceCollect(float duration)
    {
        if (collected) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FlyToPlayer(Mathf.Max(0.04f, duration)));
    }

    public void CollectImmediately()
    {
        if (collected) return;
        collected = true;
        AmmoRoomRegistry.Unregister(this, ownerRoom);
        PlayerAmmoController ammo = Object.FindAnyObjectByType<PlayerAmmoController>();
        if (ammo != null) ammo.AddReserveAmmo(amount);
        CombatImpactFXV11.EmitPickup(transform.position, new Color(0.20f, 0.92f, 1f, 1f));
        Destroy(gameObject);
    }

    private IEnumerator ScatterAndWait()
    {
        Vector3 start = transform.position;
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        Vector3 end = start + (Vector3)(direction * Random.Range(0.35f, 0.75f));
        float elapsed = 0f;
        while (elapsed < 0.28f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.28f);
            Vector3 pos = Vector3.Lerp(start, end, 1f - (1f - t) * (1f - t));
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.22f;
            transform.position = pos;
            transform.Rotate(0f, 0f, 450f * Time.unscaledDeltaTime);
            yield return null;
        }

        float wait = Random.Range(1.4f, 2.2f);
        elapsed = 0f;
        while (elapsed < wait && !collected)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!collected) yield return FlyToPlayer(0.38f);
    }

    private IEnumerator FlyToPlayer(float duration)
    {
        PlayerAmmoController ammo = Object.FindAnyObjectByType<PlayerAmmoController>();
        if (ammo == null)
        {
            CollectImmediately();
            yield break;
        }
        Vector3 start = transform.position;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration && !collected)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, ammo.transform.position, t * t * t);
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.2f, t);
            yield return null;
        }
        CollectImmediately();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!collected && other.GetComponentInParent<PlayerAmmoController>() != null)
            CollectImmediately();
    }

    private void OnDestroy()
    {
        AmmoRoomRegistry.Unregister(this, ownerRoom);
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null) return fallbackSprite;

        // V8: 임시 주황 사각형 대신 게임의 청색 네트워크 톤에 맞춘
        // 소형 에너지 카트리지 픽셀 아이콘을 런타임에 생성한다.
        const int w = 20;
        const int h = 14;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = Color.clear;
        Color outline = new Color(0.025f, 0.075f, 0.13f, 1f);
        Color metalDark = new Color(0.12f, 0.27f, 0.36f, 1f);
        Color metal = new Color(0.24f, 0.48f, 0.58f, 1f);
        Color blue = new Color(0.08f, 0.45f, 0.92f, 1f);
        Color cyan = new Color(0.08f, 0.88f, 1f, 1f);
        Color bright = new Color(0.72f, 0.98f, 1f, 1f);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, clear);

        // beveled outer cartridge body
        for (int y = 2; y <= 11; y++)
        {
            int minX = (y == 2 || y == 11) ? 3 : 2;
            int maxX = (y == 2 || y == 11) ? 16 : 17;
            for (int x = minX; x <= maxX; x++)
            {
                bool edge = x == minX || x == maxX || y == 2 || y == 11;
                tex.SetPixel(x, y, edge ? outline : metalDark);
            }
        }

        // side caps / connector teeth
        for (int y = 5; y <= 8; y++)
        {
            tex.SetPixel(1, y, outline);
            tex.SetPixel(18, y, outline);
        }
        tex.SetPixel(2, 4, metal);
        tex.SetPixel(2, 9, metal);
        tex.SetPixel(17, 4, metal);
        tex.SetPixel(17, 9, metal);

        // luminous energy window
        for (int y = 4; y <= 9; y++)
        {
            for (int x = 5; x <= 14; x++)
            {
                bool edge = x == 5 || x == 14 || y == 4 || y == 9;
                tex.SetPixel(x, y, edge ? blue : cyan);
            }
        }

        // three cell separators and highlights
        for (int y = 5; y <= 8; y++)
        {
            tex.SetPixel(8, y, blue);
            tex.SetPixel(11, y, blue);
        }
        tex.SetPixel(6, 5, bright);
        tex.SetPixel(9, 5, bright);
        tex.SetPixel(12, 5, bright);
        tex.SetPixel(15, 6, metal);
        tex.SetPixel(4, 7, metal);

        tex.Apply(false, true);
        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 46f);
        fallbackSprite.name = "AmmoEnergyCell_Runtime_V8";
        return fallbackSprite;
    }
}
