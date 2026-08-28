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

    public bool IsCollected => collected;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer.sprite == null) spriteRenderer.sprite = GetFallbackSprite();
        spriteRenderer.sortingOrder = 14;
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
        const int w = 14; const int h = 10;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = Color.clear;
        Color edge = new Color(0.45f, 0.18f, 0.02f, 1f);
        Color main = new Color(1f, 0.68f, 0.12f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, x < 1 || x >= w - 1 || y < 1 || y >= h - 1 ? edge : main);
        tex.Apply(false, true);
        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        fallbackSprite.name = "AmmoPack_Runtime";
        return fallbackSprite;
    }
}
