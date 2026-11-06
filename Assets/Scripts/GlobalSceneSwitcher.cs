using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class GlobalSceneSwitcher : MonoBehaviour
{
    private static readonly string[] SceneNames = { "StartFenster", "Schalter", "SmartHome" };

    private Font font;
    private GameObject overlayObject;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (FindAnyObjectByType<GlobalSceneSwitcher>() != null)
        {
            return;
        }

        var host = new GameObject("Global Scene Switcher");
        DontDestroyOnLoad(host);
        host.AddComponent<GlobalSceneSwitcher>();
    }

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        EnsureEventSystem();
        BuildOverlay();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (WasEscapePressed())
        {
            if (overlayObject != null && overlayObject.activeSelf)
            {
                HideOverlay(true);
            }
            else
            {
                ShowOverlay();
            }
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideOverlay(false);
        EnsureEventSystem();
        RefreshSceneButtons();
    }

    private void ShowOverlay()
    {
        if (overlayObject == null)
        {
            return;
        }

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        overlayObject.SetActive(true);
        RefreshSceneButtons();
    }

    private void HideOverlay(bool restoreCursor)
    {
        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }

        if (restoreCursor)
        {
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
        }
    }

    private void LoadScene(string sceneName)
    {
        if (SceneManager.GetActiveScene().name == sceneName)
        {
            HideOverlay(true);
            return;
        }

        HideOverlay(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        LoadingScreenController.LoadScene(sceneName);
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("Global Scene Switch Canvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        overlayObject = new GameObject("ESC Szenenwechsel Overlay");
        overlayObject.transform.SetParent(canvasObject.transform, false);
        var overlayRect = overlayObject.AddComponent<RectTransform>();
        Stretch(overlayRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var dimmer = CreatePanel("Hintergrund", overlayObject.transform, new Color(0.02f, 0.03f, 0.035f, 0.76f));
        Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var dialog = CreatePanel("Dialog", overlayObject.transform, new Color(0.94f, 0.96f, 0.96f, 0.98f));
        var dialogRect = dialog.rectTransform;
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(560f, 455f);
        dialogRect.anchoredPosition = Vector2.zero;

        CreateText("Titel", dialog.transform, "Szene wechseln", 30, FontStyle.Bold, new Color(0.08f, 0.12f, 0.15f, 1f),
            TextAnchor.MiddleCenter, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f));

        CreateText("Hinweis", dialog.transform, "ESC oeffnet und schliesst dieses Menue.", 16, FontStyle.Normal, new Color(0.30f, 0.36f, 0.40f, 1f),
            TextAnchor.MiddleCenter, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.79f));

        for (var index = 0; index < SceneNames.Length; index++)
        {
            var sceneName = SceneNames[index];
            var yMax = 0.64f - index * 0.18f;
            var yMin = yMax - 0.12f;
            var button = CreateButton("Szene " + sceneName, dialog.transform, sceneName, new Color(0.10f, 0.46f, 0.40f, 1f),
                new Vector2(0.16f, yMin), new Vector2(0.84f, yMax));
            button.onClick.AddListener(() => LoadScene(sceneName));
        }

        var closeButton = CreateButton("Schliessen", dialog.transform, "Schliessen", new Color(0.36f, 0.40f, 0.44f, 1f),
            new Vector2(0.28f, 0.07f), new Vector2(0.72f, 0.18f));
        closeButton.onClick.AddListener(() => HideOverlay(true));

        overlayObject.SetActive(false);
    }

    private void RefreshSceneButtons()
    {
        if (overlayObject == null)
        {
            return;
        }

        var activeScene = SceneManager.GetActiveScene().name;
        foreach (var button in overlayObject.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<Text>();
            if (label == null)
            {
                continue;
            }

            foreach (var sceneName in SceneNames)
            {
                if (button.gameObject.name == "Szene " + sceneName)
                {
                    label.text = sceneName == activeScene ? sceneName + " (aktuell)" : sceneName;
                    break;
                }
            }
        }
    }

    private void EnsureEventSystem()
    {
        var eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            DontDestroyOnLoad(eventSystemObject);
        }

        foreach (var duplicate in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (duplicate != eventSystem)
            {
                Destroy(duplicate.gameObject);
            }
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneInputModule != null)
        {
            standaloneInputModule.enabled = false;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
#else
        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif
    }

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private Image CreatePanel(string name, Transform parent, Color color)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        var rect = panel.AddComponent<RectTransform>();
        rect.localScale = Vector3.one;
        var image = panel.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        var rect = textObject.AddComponent<RectTransform>();
        Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var image = CreatePanel(name, parent, color);
        Stretch(image.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        CreateText("Label", image.transform, label, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));

        return button;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
