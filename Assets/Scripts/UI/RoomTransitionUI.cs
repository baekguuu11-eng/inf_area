using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomTransitionUI : MonoBehaviour
{
    [System.Serializable]
    public class RoomLabelOverride
    {
        public int stageNumber = 1;
        public int roomNumber = 1;
        [TextArea] public string labelText;
    }

    [Header("References")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private TMP_Text roomLabelText;

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 0.18f;
    [SerializeField] private float fadeInDuration = 0.18f;

    [Header("Room Label Settings")]
    [SerializeField] private float roomLabelFadeInDuration = 0.12f;
    [SerializeField] private float roomLabelHoldDuration = 0.65f;
    [SerializeField] private float roomLabelFadeOutDuration = 0.22f;
    [SerializeField] private List<RoomLabelOverride> roomLabelOverrides = new();

    public float FadeOutDuration => fadeOutDuration;
    public float FadeInDuration => fadeInDuration;

    private void Awake()
    {
        if (fadePanel != null)
            SetFadeAlpha(0f);

        if (roomLabelText != null)
        {
            roomLabelText.text = string.Empty;
            SetLabelAlpha(0f);
        }
    }

    public void SetFadeAlpha(float alpha)
    {
        if (fadePanel == null)
            return;

        Color color = fadePanel.color;
        color.a = Mathf.Clamp01(alpha);
        fadePanel.color = color;
    }

    public string GetRoomLabel(int stage, int room)
    {
        for (int i = 0; i < roomLabelOverrides.Count; i++)
        {
            RoomLabelOverride item = roomLabelOverrides[i];

            if (item.stageNumber == stage &&
                item.roomNumber == room &&
                !string.IsNullOrWhiteSpace(item.labelText))
            {
                return item.labelText;
            }
        }

        return GetFallbackRoomLabel(stage, room);
    }

    private string GetFallbackRoomLabel(int stage, int room)
    {
        // 기본값은 자동으로 1-1, 1-2, 2-1 ... 형태로 표시
        // 필요하면 여기서 코드로 직접 바꿔도 됨
        // 예:
        // if (stage == 1 && room == 1) return "1-1 시작 구역";
        // if (stage == 1 && room == 5) return "1-5 포탈방";
        if (stage == 1 && room == 5) return "1-5 Portal Room";
        return $"{stage}-{room}";
    }

    public IEnumerator ShowRoomLabel(int stage, int room)
    {
        if (roomLabelText == null)
            yield break;

        roomLabelText.text = GetRoomLabel(stage, room);

        yield return FadeLabel(0f, 1f, roomLabelFadeInDuration);
        yield return new WaitForSecondsRealtime(roomLabelHoldDuration);
        yield return FadeLabel(1f, 0f, roomLabelFadeOutDuration);
    }

    private IEnumerator FadeLabel(float fromAlpha, float toAlpha, float duration)
    {
        if (roomLabelText == null)
            yield break;

        if (duration <= 0f)
        {
            SetLabelAlpha(toAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            SetLabelAlpha(alpha);
            yield return null;
        }

        SetLabelAlpha(toAlpha);
    }

    private void SetLabelAlpha(float alpha)
    {
        if (roomLabelText == null)
            return;

        Color color = roomLabelText.color;
        color.a = Mathf.Clamp01(alpha);
        roomLabelText.color = color;
    }
}