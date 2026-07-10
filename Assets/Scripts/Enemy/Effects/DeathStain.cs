using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DeathStain : MonoBehaviour
{
    [SerializeField] private float lifeTime = 1.6f;
    [SerializeField] private float fadeDuration = 0.6f;

    private SpriteRenderer sr;
    private Color startColor;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        startColor = sr.color;
    }

    private void Start()
    {
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float waitTime = Mathf.Max(0f, lifeTime - fadeDuration);
        yield return new WaitForSeconds(waitTime);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            sr.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}