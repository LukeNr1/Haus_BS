using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoadingScreenController : MonoBehaviour
{
    private const float MinimumLoadingTime = 2.0f;
    private static bool isLoading;

    private readonly string[] messages =
    {
        "Schalterraum wird vorbereitet",
        "X20 LEDs werden geweckt",
        "Signale sortieren sich",
        "Gleich geht's weiter"
    };

    private string targetSceneName;
    private CanvasGroup canvasGroup;
    private Image progressFill;
    private Text statusText;
    private Text percentText;

    public static void LoadScene(string sceneName)
    {
        if (isLoading)
        {
            return;
        }

        var host = new GameObject("Relaxed Loading Screen");
        DontDestroyOnLoad(host);

        var controller = host.AddComponent<LoadingScreenController>();
        controller.Begin(sceneName);
    }

    private void Awake()
    {
        isLoading = true;
        BuildView();
    }

    private void Begin(string sceneName)
    {
        targetSceneName = sceneName;
        StartCoroutine(LoadRoutine());
    }

    private void OnDestroy()
    {
        isLoading = false;
    }

    private IEnumerator LoadRoutine()
    {
        var operation = SceneManager.LoadSceneAsync(targetSceneName);
        operation.allowSceneActivation = false;

        var elapsed = 0f;
        var messageIndex = -1;

        while (elapsed < MinimumLoadingTime || operation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            var loadProgress = Mathf.Clamp01(operation.progress / 0.9f);
            var timeProgress = Mathf.Clamp01(elapsed / MinimumLoadingTime);
            var visibleProgress = Mathf.Min(0.98f, Mathf.Max(loadProgress, timeProgress * 0.88f));
            SetProgress(visibleProgress);

            var nextMessageIndex = Mathf.FloorToInt(elapsed / 0.65f) % messages.Length;
            if (nextMessageIndex != messageIndex)
            {
                messageIndex = nextMessageIndex;
                statusText.text = messages[messageIndex];
            }

            yield return null;
        }

        SetProgress(1f);
        statusText.text = "Alles bereit.";
        yield return new WaitForSecondsRealtime(0.25f);

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }

        yield return FadeOut();
        Destroy(gameObject);
    }

    private IEnumerator FadeOut()
    {
        var duration = 0.18f;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
    }

    private void SetProgress(float value)
    {
        var clampedValue = Mathf.Clamp01(value);
        progressFill.fillAmount = clampedValue;
        percentText.text = Mathf.RoundToInt(clampedValue * 100f) + "%";
    }

    private void BuildView()
    {
        var canvasObject = new GameObject("Loading Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();

        var root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var background = CreateImage("Background", root, new Color(0.035f, 0.047f, 0.052f, 1f));
        Stretch(background);

        var accent = CreateImage("Accent Bar", root, new Color(1f, 0.5f, 0.12f, 1f));
        SetAnchoredRect(accent, new Vector2(0.5f, 0.55f), new Vector2(620f, 4f), new Vector2(0f, 148f));

        var title = CreateText("Title", root, "Kurz locker bleiben", 38, FontStyle.Bold, new Color(0.96f, 0.98f, 0.95f, 1f), TextAnchor.MiddleCenter);
        SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720f, 70f), new Vector2(0f, 95f));

        statusText = CreateText("Status", root, messages[0], 20, FontStyle.Normal, new Color(0.72f, 0.82f, 0.78f, 1f), TextAnchor.MiddleCenter);
        SetAnchoredRect(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720f, 42f), new Vector2(0f, 36f));

        var track = CreateImage("Progress Track", root, new Color(0.14f, 0.18f, 0.18f, 1f));
        SetAnchoredRect(track, new Vector2(0.5f, 0.5f), new Vector2(560f, 14f), new Vector2(0f, -22f));

        progressFill = CreateImage("Progress Fill", track, new Color(1f, 0.55f, 0.16f, 1f)).GetComponent<Image>();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
        Stretch(progressFill.rectTransform);

        percentText = CreateText("Percent", root, "0%", 18, FontStyle.Bold, new Color(0.96f, 0.9f, 0.78f, 1f), TextAnchor.MiddleCenter);
        SetAnchoredRect(percentText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120f, 36f), new Vector2(0f, -62f));
    }

    private static RectTransform CreateImage(string name, Transform parent, Color color)
    {
        var rectTransform = CreateUiObject(name, parent);
        var image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private static Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        var rectTransform = CreateUiObject(name, parent);
        var text = rectTransform.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = GetDefaultFont();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static RectTransform CreateUiObject(string name, Transform parent)
    {
        var uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject.GetComponent<RectTransform>();
    }

    private static Font GetDefaultFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredRect(RectTransform rectTransform, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
    }
}
