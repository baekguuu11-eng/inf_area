using System.Collections;
using TMPro;
using UnityEngine;

public class MainMenuSystemStatusDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI networkText;
    [SerializeField] private TextMeshProUGUI rafaelText;
    [SerializeField] private TextMeshProUGUI buildText;

    [Header("Text")]
    [SerializeField] private string networkPrefix = "[ARK NETWORK]  CONNECTION : ";
    [SerializeField] private string rafaelPrefix = "[RAFAEL NODE]  STATUS : ";
    [SerializeField] private string buildLabel = "BUILD 0.1 // LOCAL NODE";

    [Header("Timing")]
    [SerializeField] private float minStatusChangeInterval = 8f;
    [SerializeField] private float maxStatusChangeInterval = 15f;
    [SerializeField] private float microFlickerDuration = 0.045f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.56f, 0.88f, 0.92f, 0.72f);
    [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.18f, 0.82f);

    private readonly string[] networkStates =
    {
        "UNSTABLE",
        "PACKET LOSS 02%",
        "SYNC DEGRADED",
        "UNSTABLE",
        "UNSTABLE"
    };

    private readonly string[] nodeStates =
    {
        "STANDBY",
        "STANDBY",
        "CORE IDLE",
        "STANDBY"
    };

    private Coroutine statusRoutine;

    private void OnEnable()
    {
        ApplyInitialText();

        if (statusRoutine != null)
            StopCoroutine(statusRoutine);

        statusRoutine = StartCoroutine(StatusRoutine());
    }

    private void OnDisable()
    {
        if (statusRoutine != null)
        {
            StopCoroutine(statusRoutine);
            statusRoutine = null;
        }
    }

    private void ApplyInitialText()
    {
        if (networkText != null)
        {
            networkText.text = networkPrefix + "UNSTABLE";
            networkText.color = warningColor;
        }

        if (rafaelText != null)
        {
            rafaelText.text = rafaelPrefix + "STANDBY";
            rafaelText.color = normalColor;
        }

        if (buildText != null)
        {
            buildText.text = buildLabel;
            buildText.color = normalColor;
        }
    }

    private IEnumerator StatusRoutine()
    {
        while (true)
        {
            float wait = Random.Range(
                Mathf.Max(1f, minStatusChangeInterval),
                Mathf.Max(minStatusChangeInterval, maxStatusChangeInterval)
            );

            yield return new WaitForSecondsRealtime(wait);

            yield return MicroFlicker();

            string network = networkStates[
                Random.Range(0, networkStates.Length)
            ];

            string node = nodeStates[
                Random.Range(0, nodeStates.Length)
            ];

            if (networkText != null)
            {
                networkText.text = networkPrefix + network;
                networkText.color = network == "UNSTABLE"
                    ? warningColor
                    : normalColor;
            }

            if (rafaelText != null)
                rafaelText.text = rafaelPrefix + node;
        }
    }

    private IEnumerator MicroFlicker()
    {
        float oldNetworkAlpha = networkText != null
            ? networkText.alpha
            : 1f;

        float oldRafaelAlpha = rafaelText != null
            ? rafaelText.alpha
            : 1f;

        if (networkText != null)
            networkText.alpha = 0.25f;

        if (rafaelText != null)
            rafaelText.alpha = 0.35f;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0.01f, microFlickerDuration)
        );

        if (networkText != null)
            networkText.alpha = oldNetworkAlpha;

        if (rafaelText != null)
            rafaelText.alpha = oldRafaelAlpha;
    }

    private void OnValidate()
    {
        minStatusChangeInterval = Mathf.Max(
            1f,
            minStatusChangeInterval
        );

        maxStatusChangeInterval = Mathf.Max(
            minStatusChangeInterval,
            maxStatusChangeInterval
        );

        microFlickerDuration = Mathf.Max(
            0.01f,
            microFlickerDuration
        );
    }
}
