using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyDeathShard : MonoBehaviour
{
    [SerializeField] private float minForce = 3f;
    [SerializeField] private float maxForce = 6f;
    [SerializeField] private float maxTorque = 500f;
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float fadeStartTime = 0.4f;
    [SerializeField] private float startScale = 0.28f;
    [SerializeField] private float endScale = 0.08f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.one * startScale;
    }

    public void Launch(Vector2 biasDirection, float biasStrength)
    {
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        if (randomDir == Vector2.zero)
            randomDir = Vector2.up;

        Vector2 finalDir = randomDir;

        if (biasDirection != Vector2.zero)
            finalDir = (randomDir + biasDirection.normalized * biasStrength).normalized;

        float force = Random.Range(minForce, maxForce);
        rb.AddForce(finalDir * force, ForceMode2D.Impulse);

        float torque = Random.Range(-maxTorque, maxTorque);
        rb.AddTorque(torque);

        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        Color startColor = sr.color;
        float elapsed = 0f;

        while (elapsed < lifeTime)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= fadeStartTime)
            {
                float t = Mathf.InverseLerp(fadeStartTime, lifeTime, elapsed);

                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;

                float scale = Mathf.Lerp(startScale, endScale, t);
                transform.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}