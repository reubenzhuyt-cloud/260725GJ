using System.Collections;
using TMPro;
using UnityEngine;

public class NoticePanel : MonoBehaviour
{
    private const float StayDuration = 1f;
    private const float FadeMoveDuration = 0.8f;

    [SerializeField] private TMP_Text contentText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private float moveDistance = 100f;

    private Vector2 startPosition;

    public IEnumerator Play(string content)
    {
        if (contentText != null)
            contentText.text = content;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (rectTransform != null)
            startPosition = rectTransform.anchoredPosition;

        float stayElapsed = 0f;
        while (stayElapsed < StayDuration)
        {
            stayElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float fadeElapsed = 0f;
        while (fadeElapsed < FadeMoveDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeElapsed / FadeMoveDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            if (rectTransform != null)
                rectTransform.anchoredPosition = startPosition + new Vector2(0f, moveDistance * smooth);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - smooth;

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}
