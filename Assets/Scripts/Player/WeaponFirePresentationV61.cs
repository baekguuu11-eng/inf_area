using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// v6.1 원거리 무기 시각 피드백.
/// 총구 섬광, 순간광, 탄피/에너지 배출을 외부 아트 없이 픽셀 형태로 표현한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponFirePresentationV61 : MonoBehaviour
{
    private const int MaximumCasings = 48;
    private const int MaximumFlashes = 18;

    private static readonly Stack<CasingFxV61> casingPool = new Stack<CasingFxV61>();
    private static readonly Stack<MuzzleFlashFxV61> flashPool = new Stack<MuzzleFlashFxV61>();
    private static readonly List<CasingFxV61> activeCasings = new List<CasingFxV61>();
    private static Transform poolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPools()
    {
        casingPool.Clear();
        flashPool.Clear();
        activeCasings.Clear();
        poolRoot = null;
    }

    public void PlayFire(WeaponDefinition weapon, WeaponVisualController visuals, Vector2 aimDirection)
    {
        if (weapon == null || visuals == null)
            return;

        SpawnMuzzleFlash(weapon, visuals.GetMuzzleWorldPosition(), aimDirection);

        if (IsLaser(weapon))
        {
            SpawnEnergyVent(weapon, visuals);
        }
        else if (IsShotgun(weapon))
        {
            StartCoroutine(DelayedCasing(weapon, visuals, 0.11f));
        }
        else
        {
            SpawnCasing(weapon, visuals);
        }

    }

    private IEnumerator DelayedCasing(WeaponDefinition weapon, WeaponVisualController visuals, float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
        if (weapon != null && visuals != null && visuals.isActiveAndEnabled)
            SpawnCasing(weapon, visuals);
    }

    private static void SpawnCasing(WeaponDefinition weapon, WeaponVisualController visuals)
    {
        EnsurePoolRoot();
        TrimOldCasings();

        CasingFxV61 effect = GetCasingFromPool();
        effect.gameObject.SetActive(true);
        effect.transform.SetParent(poolRoot, false);
        effect.transform.position = visuals.GetCasingEjectWorldPosition();
        effect.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        bool shotgun = IsShotgun(weapon);
        bool machineGun = IsMachineGun(weapon);
        float playerHeight = visuals.PlayerWorldHeight;
        float size = playerHeight * (shotgun ? 0.050f : machineGun ? 0.030f : 0.036f);
        float speed = shotgun ? 1.10f : machineGun ? 1.95f : 1.65f;
        float lifetime = shotgun ? 2.8f : machineGun ? 1.65f : 2.25f;
        Vector2 direction = visuals.GetCasingEjectWorldDirection();
        direction = (Quaternion.Euler(0f, 0f, Random.Range(-16f, 16f)) * direction).normalized;

        Color color = shotgun
            ? new Color(0.78f, 0.18f, 0.10f, 1f)
            : new Color(1.00f, 0.72f, 0.16f, 1f);

        effect.Configure(
            RuntimePixelSpriteFactory.GetCasingSprite(shotgun),
            color,
            size,
            direction * Random.Range(speed * 0.86f, speed * 1.14f),
            Random.Range(-720f, 720f),
            lifetime,
            ReturnCasing);
        activeCasings.Add(effect);
    }

    private static void SpawnEnergyVent(WeaponDefinition weapon, WeaponVisualController visuals)
    {
        EnsurePoolRoot();
        int count = 3;
        for (int i = 0; i < count; i++)
        {
            TrimOldCasings();
            CasingFxV61 effect = GetCasingFromPool();
            effect.gameObject.SetActive(true);
            effect.transform.SetParent(poolRoot, false);
            effect.transform.position = visuals.GetCasingEjectWorldPosition();
            effect.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector2 direction = visuals.GetCasingEjectWorldDirection();
            direction = (Quaternion.Euler(0f, 0f, Random.Range(-34f, 34f)) * direction).normalized;
            float size = visuals.PlayerWorldHeight * Random.Range(0.020f, 0.034f);
            Color color = Color.Lerp(weapon.accentColor, Color.white, Random.Range(0.15f, 0.55f));
            color *= 1.55f;
            color.a = 0.95f;

            effect.Configure(
                RuntimePixelSpriteFactory.GetEnergySparkSprite(),
                color,
                size,
                direction * Random.Range(0.9f, 1.7f),
                Random.Range(-560f, 560f),
                Random.Range(0.28f, 0.46f),
                ReturnCasing);
            activeCasings.Add(effect);
        }
    }

    private static void SpawnMuzzleFlash(WeaponDefinition weapon, Vector2 position, Vector2 aimDirection)
    {
        EnsurePoolRoot();
        MuzzleFlashFxV61 effect = GetFlashFromPool();
        effect.gameObject.SetActive(true);
        effect.transform.SetParent(poolRoot, false);
        effect.transform.position = position;
        effect.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg + Random.Range(-4f, 4f));

        bool shotgun = IsShotgun(weapon);
        bool machineGun = IsMachineGun(weapon);
        bool laser = IsLaser(weapon);
        // v6.1.5: keep the muzzle cue outside the face and small enough for the 320x180 pixel camera.
        float size = shotgun ? 0.20f : laser ? 0.125f : machineGun ? 0.0875f : 0.125f;
        float intensity = shotgun ? 0.50f : laser ? 0.32f : machineGun ? 0.16f : 0.24f;
        float radius = shotgun ? 0.42f : laser ? 0.28f : machineGun ? 0.16f : 0.22f;
        float lifetime = shotgun ? 0.040f : laser ? 0.035f : machineGun ? 0.024f : 0.030f;
        Color color = laser ? weapon.accentColor : new Color(1f, 0.70f, 0.22f, 1f);
        color *= laser ? 1.75f : 1.45f;
        color.a = 1f;

        effect.Configure(color, size, intensity, radius, lifetime, ReturnFlash);
    }

    private static CasingFxV61 GetCasingFromPool()
    {
        CasingFxV61 effect = null;
        while (casingPool.Count > 0 && effect == null)
            effect = casingPool.Pop();
        return effect != null ? effect : CreateCasing();
    }

    private static MuzzleFlashFxV61 GetFlashFromPool()
    {
        MuzzleFlashFxV61 effect = null;
        while (flashPool.Count > 0 && effect == null)
            effect = flashPool.Pop();
        return effect != null ? effect : CreateFlash();
    }

    private static CasingFxV61 CreateCasing()
    {
        GameObject gameObject = new GameObject("V61_CasingFx");
        gameObject.hideFlags = HideFlags.DontSave;
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 24;
        V620SpriteMaterialUtility.Apply(renderer);
        CasingFxV61 effect = gameObject.AddComponent<CasingFxV61>();
        effect.Initialize(renderer);
        return effect;
    }

    private static MuzzleFlashFxV61 CreateFlash()
    {
        GameObject gameObject = new GameObject("V61_MuzzleFlashFx");
        gameObject.hideFlags = HideFlags.DontSave;
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimePixelSpriteFactory.GetMuzzleFlashSprite();
        renderer.sortingOrder = 22;
        V620SpriteMaterialUtility.Apply(renderer);
        Light2D light = gameObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        // Renderer2D의 0번 Blend Style은 Multiply라 발사 순간 캐릭터와 총이 검게 변한다.
        // v6.1.1에서는 Additive로 구성된 1번 Blend Style을 명시적으로 사용한다.
        light.blendStyleIndex = 1;
        light.intensity = 0f;
        light.enabled = false;
        MuzzleFlashFxV61 effect = gameObject.AddComponent<MuzzleFlashFxV61>();
        effect.Initialize(renderer, light);
        return effect;
    }

    private static void ReturnCasing(CasingFxV61 effect)
    {
        if (effect == null)
            return;
        activeCasings.Remove(effect);
        effect.gameObject.SetActive(false);
        effect.transform.SetParent(poolRoot, false);
        if (casingPool.Count < MaximumCasings)
            casingPool.Push(effect);
        else
            Object.Destroy(effect.gameObject);
    }

    private static void ReturnFlash(MuzzleFlashFxV61 effect)
    {
        if (effect == null)
            return;
        effect.gameObject.SetActive(false);
        effect.transform.SetParent(poolRoot, false);
        if (flashPool.Count < MaximumFlashes)
            flashPool.Push(effect);
        else
            Object.Destroy(effect.gameObject);
    }

    private static void TrimOldCasings()
    {
        for (int i = activeCasings.Count - 1; i >= 0; i--)
        {
            if (activeCasings[i] == null || !activeCasings[i].gameObject.activeSelf)
                activeCasings.RemoveAt(i);
        }

        while (activeCasings.Count >= MaximumCasings)
        {
            CasingFxV61 oldest = activeCasings[0];
            activeCasings.RemoveAt(0);
            if (oldest != null)
                oldest.ReleaseImmediate();
        }
    }

    private static void EnsurePoolRoot()
    {
        if (poolRoot != null)
            return;
        GameObject root = new GameObject("V61_WeaponFxPool");
        root.hideFlags = HideFlags.DontSave;
        poolRoot = root.transform;
    }

    private static bool IsLaser(WeaponDefinition weapon)
    {
        return weapon != null && (weapon.rangedMode == RangedAttackMode.HitscanBeam ||
            (!string.IsNullOrEmpty(weapon.weaponId) && weapon.weaponId.ToLowerInvariant().Contains("laser")));
    }

    private static bool IsShotgun(WeaponDefinition weapon)
    {
        return weapon != null && (weapon.rangedMode == RangedAttackMode.Shotgun ||
            (!string.IsNullOrEmpty(weapon.weaponId) && weapon.weaponId.ToLowerInvariant().Contains("shotgun")));
    }

    private static bool IsMachineGun(WeaponDefinition weapon)
    {
        return weapon != null && !string.IsNullOrEmpty(weapon.weaponId) &&
            weapon.weaponId.ToLowerInvariant().Contains("machine");
    }
}

public sealed class CasingFxV61 : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;
    private float angularVelocity;
    private float lifetime;
    private float elapsed;
    private float startScale;
    private Color startColor;
    private System.Action<CasingFxV61> release;

    public void Initialize(SpriteRenderer renderer)
    {
        spriteRenderer = renderer;
    }

    public void Configure(Sprite sprite, Color color, float worldSize, Vector2 initialVelocity,
        float spin, float life, System.Action<CasingFxV61> releaseCallback)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        V620SpriteMaterialUtility.Apply(spriteRenderer);
        spriteRenderer.color = color;
        startColor = color;
        startScale = worldSize / Mathf.Max(0.0001f, sprite.bounds.size.x);
        transform.localScale = Vector3.one * startScale;
        velocity = initialVelocity;
        angularVelocity = spin;
        lifetime = Mathf.Max(0.08f, life);
        elapsed = 0f;
        release = releaseCallback;
        enabled = true;
    }

    public void ReleaseImmediate()
    {
        enabled = false;
        if (release != null)
            release(this);
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        elapsed += delta;
        transform.position += (Vector3)(velocity * delta);
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, 2.8f * delta);
        transform.Rotate(0f, 0f, angularVelocity * delta);
        angularVelocity = Mathf.MoveTowards(angularVelocity, 0f, 180f * delta);

        float normalized = Mathf.Clamp01(elapsed / lifetime);
        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, Mathf.InverseLerp(0.62f, 1f, normalized));
            spriteRenderer.color = color;
        }
        transform.localScale = Vector3.one * (startScale * Mathf.Lerp(1f, 0.72f, normalized));

        if (elapsed >= lifetime)
            ReleaseImmediate();
    }
}

public sealed class MuzzleFlashFxV61 : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Light2D pointLight;
    private float lifetime;
    private float elapsed;
    private float startScale;
    private Color startColor;
    private System.Action<MuzzleFlashFxV61> release;

    public void Initialize(SpriteRenderer renderer, Light2D light)
    {
        spriteRenderer = renderer;
        pointLight = light;
    }

    public void Configure(Color color, float worldSize, float lightIntensity, float lightRadius,
        float life, System.Action<MuzzleFlashFxV61> releaseCallback)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (pointLight == null)
            pointLight = GetComponent<Light2D>();

        spriteRenderer.sprite = RuntimePixelSpriteFactory.GetMuzzleFlashSprite();
        V620SpriteMaterialUtility.Apply(spriteRenderer);
        spriteRenderer.color = color;
        startColor = color;
        startScale = worldSize / Mathf.Max(0.0001f, spriteRenderer.sprite.bounds.size.x);
        transform.localScale = Vector3.one * startScale;

        if (pointLight != null)
        {
            // v6.2: Renderer2D Blend Style 설정에 따라 화면이 검게 변하는 문제를 원천 차단한다.
            pointLight.intensity = 0f;
            pointLight.enabled = false;
        }

        lifetime = Mathf.Max(0.02f, life);
        elapsed = 0f;
        release = releaseCallback;
        enabled = true;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        float alpha = 1f - t;
        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a = alpha;
            spriteRenderer.color = color;
        }
        if (pointLight != null)
            pointLight.intensity = 0f;
        transform.localScale = Vector3.one * (startScale * Mathf.Lerp(1f, 0.62f, t));

        if (elapsed >= lifetime)
        {
            enabled = false;
            if (release != null)
                release(this);
        }
    }
}
