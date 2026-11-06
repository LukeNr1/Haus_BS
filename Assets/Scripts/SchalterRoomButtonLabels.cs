using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SchalterRoomButtonLabels : MonoBehaviour
{
    private const string SceneName = "Schalter";

    private readonly List<LabelBinding> labels = new();
    private Font font;
    private RectTransform canvasRect;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForCurrentScene()
    {
        TryCreateForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForScene(scene);
    }

    private static void TryCreateForScene(Scene scene)
    {
        if (scene.name != SceneName || FindAnyObjectByType<SchalterRoomButtonLabels>() != null)
        {
            return;
        }

        var host = new GameObject("Schalter Raum Beschriftungen");
        host.AddComponent<SchalterRoomButtonLabels>();
    }

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        BuildCanvas();
        CreateLabels();
    }

    private void LateUpdate()
    {
        if (labels.Count == 0)
        {
            CreateLabels();
        }

        var camera = Camera.main;
        if (camera == null || canvasRect == null)
        {
            return;
        }

        foreach (var label in labels)
        {
            if (label.Target == null)
            {
                label.Root.SetActive(false);
                continue;
            }

            var worldPosition = GetLabelWorldPosition(label.Target);
            var screenPosition = camera.WorldToScreenPoint(worldPosition);
            var isVisible = screenPosition.z > 0f;
            label.Root.SetActive(isVisible);

            if (!isVisible)
            {
                continue;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var localPoint);
            label.Rect.anchoredPosition = localPoint + new Vector2(0f, 22f);
        }
    }

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("Schalter Raum Label Canvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 650;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasObject.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            canvasRect = canvasObject.AddComponent<RectTransform>();
        }

        Stretch(canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private void CreateLabels()
    {
        labels.Clear();
        AddLabelFor("schalter1", new RoomVisual("RAUM 1", new Color(0.78f, 0.08f, 0.05f, 0.94f), Color.white));
        AddLabelFor("schalter2", new RoomVisual("RAUM 2", new Color(0.07f, 0.24f, 0.85f, 0.94f), Color.white));
        AddLabelFor("schalter3", new RoomVisual("RAUM 3", new Color(0.96f, 0.84f, 0.05f, 0.96f), new Color(0.06f, 0.06f, 0.05f, 1f)));
    }

    private void AddLabelFor(string targetName, RoomVisual fallback)
    {
        var target = GameObject.Find(targetName);
        if (target == null)
        {
            return;
        }

        var visual = ResolveVisual(target, fallback);
        var root = new GameObject(visual.Text + " Label");
        root.transform.SetParent(canvasRect, false);

        var rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(150f, 42f);

        var background = root.AddComponent<Image>();
        background.color = visual.BackgroundColor;
        background.raycastTarget = false;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(root.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));

        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = visual.Text;
        text.fontSize = 25;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = visual.TextColor;
        text.raycastTarget = false;

        var outline = textObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.effectColor = visual.TextColor == Color.white ? new Color(0f, 0f, 0f, 0.75f) : new Color(1f, 1f, 1f, 0.35f);

        labels.Add(new LabelBinding(target, root, rect));
    }

    private static RoomVisual ResolveVisual(GameObject target, RoomVisual fallback)
    {
        if (!TryFindAccentColor(target, out var color))
        {
            return fallback;
        }

        Color.RGBToHSV(color, out var hue, out var saturation, out var value);
        if (saturation < 0.2f || value < 0.25f)
        {
            return fallback;
        }

        if (color.b > color.r && color.b > color.g)
        {
            return new RoomVisual("RAUM 2", new Color(0.07f, 0.24f, 0.85f, 0.94f), Color.white);
        }

        if ((hue > 0.10f && hue < 0.20f) || (color.r > 0.75f && color.g > 0.55f && color.b < 0.35f))
        {
            return new RoomVisual("RAUM 3", new Color(0.96f, 0.84f, 0.05f, 0.96f), new Color(0.06f, 0.06f, 0.05f, 1f));
        }

        if (color.r > color.g && color.r > color.b)
        {
            return new RoomVisual("RAUM 1", new Color(0.78f, 0.08f, 0.05f, 0.94f), Color.white);
        }

        return fallback;
    }

    private static bool TryFindAccentColor(GameObject target, out Color accentColor)
    {
        accentColor = Color.clear;
        var bestScore = 0f;
        var renderers = target.GetComponentsInChildren<Renderer>(true);

        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || !TryGetMaterialColor(material, out var color))
                {
                    continue;
                }

                Color.RGBToHSV(color, out _, out var saturation, out var value);
                var score = saturation * value;
                if (score > bestScore)
                {
                    bestScore = score;
                    accentColor = color;
                }
            }
        }

        return bestScore > 0.15f;
    }

    private static bool TryGetMaterialColor(Material material, out Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            return true;
        }

        color = Color.clear;
        return false;
    }

    private static Vector3 GetLabelWorldPosition(GameObject target)
    {
        if (TryGetBounds(target, out var bounds))
        {
            return new Vector3(bounds.center.x, bounds.max.y + Mathf.Max(0.28f, bounds.size.y * 0.45f), bounds.center.z);
        }

        return target.transform.position + Vector3.up * 0.75f;
    }

    private static bool TryGetBounds(GameObject target, out Bounds bounds)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = new Bounds(target.transform.position, Vector3.one);
            return false;
        }

        bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private readonly struct RoomVisual
    {
        public RoomVisual(string text, Color backgroundColor, Color textColor)
        {
            Text = text;
            BackgroundColor = backgroundColor;
            TextColor = textColor;
        }

        public string Text { get; }
        public Color BackgroundColor { get; }
        public Color TextColor { get; }
    }

    private readonly struct LabelBinding
    {
        public LabelBinding(GameObject target, GameObject root, RectTransform rect)
        {
            Target = target;
            Root = root;
            Rect = rect;
        }

        public GameObject Target { get; }
        public GameObject Root { get; }
        public RectTransform Rect { get; }
    }
}
