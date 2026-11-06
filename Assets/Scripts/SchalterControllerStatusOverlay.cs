using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Opc.UaFx;
using Opc.UaFx.Client;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SchalterControllerStatusOverlay : MonoBehaviour
{
    private const string SceneName = "Schalter";
    private const string OpcUaEndpointUrl = "opc.tcp://192.168.1.61:4840";

    private readonly StatusNode[] nodes =
    {
        new("ServerDiagnostics/.../CurrentSessionCount", "CurrentSession...", "i=2277"),
        new("Server/ServerStatus/CurrentTime", "CurrentTime", "i=2258"),
        new("Server/ServerStatus/State", "State", "i=2259"),
        new("ServerStatus/BuildInfo/SoftwareVersion", "SoftwareVersion", "i=2264"),
        new("ServerStatus/BuildInfo/ManufacturerName", "ManufacturerN...", "i=2263"),
        new("ServerStatus/BuildInfo/BuildDate", "BuildDate", "i=2265"),
        new("Server/ServerArray", "ServerArray", "i=2254"),
        new("Resources/CPU/Model", "Model", null),
        new("Resources/CPU/SerialNumber", "SerialNumber", null)
    };

    private readonly List<RowBinding> rows = new();
    private Font font;
    private GameObject panelObject;
    private bool isReading;

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
        if (scene.name != SceneName)
        {
            return;
        }

        if (FindAnyObjectByType<SchalterControllerStatusOverlay>() != null)
        {
            return;
        }

        var host = new GameObject("Schalter Controller Status Overlay");
        host.AddComponent<SchalterControllerStatusOverlay>();
    }

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        BuildOverlay();
        panelObject.SetActive(true);
        SetLoadingValues("Statuswerte werden automatisch geladen.");
    }

    private void Start()
    {
        ShowAndRefresh();
    }

    private async void ShowAndRefresh()
    {
        if (isReading)
        {
            return;
        }

        panelObject.SetActive(true);
        SetLoadingValues("Verbinde mit " + OpcUaEndpointUrl + " ...");

        isReading = true;
        try
        {
            var values = await Task.Run(ReadOpcValues);
            if (this == null || panelObject == null)
            {
                return;
            }

            ApplyValues(values);
        }
        catch (Exception exception)
        {
            if (this == null || panelObject == null)
            {
                return;
            }

            Debug.LogWarning("Schalter Statuswerte konnten nicht gelesen werden: " + exception.Message);
            ApplyValues(CreateUnavailableValues("OPC-UA nicht erreichbar: " + exception.Message));
        }
        finally
        {
            isReading = false;
        }
    }

    private Dictionary<string, string> ReadOpcValues()
    {
        using var client = new OpcClient(OpcUaEndpointUrl);
        client.Security.UserIdentity = new OpcClientIdentity("opcuser1", ".opcuser1");
        client.Connect();

        var values = new Dictionary<string, string>();
        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.NodeId))
            {
                values[node.DisplayName] = "NodeId fehlt";
                continue;
            }

            try
            {
                values[node.DisplayName] = FormatOpcValue(client.ReadNode(node.NodeId).Value);
            }
            catch (Exception exception)
            {
                values[node.DisplayName] = ShortMessage(exception);
            }
        }

        return values;
    }

    private Dictionary<string, string> CreateUnavailableValues(string message)
    {
        var values = new Dictionary<string, string>();
        foreach (var node in nodes)
        {
            values[node.DisplayName] = "Lade...";
        }

        values[nodes[0].DisplayName] = message;
        return values;
    }

    private void SetLoadingValues(string firstMessage)
    {
        ApplyValues(CreateUnavailableValues(firstMessage));
    }

    private void ApplyValues(Dictionary<string, string> values)
    {
        foreach (var row in rows)
        {
            if (row.ValueText == null)
            {
                continue;
            }

            row.ValueText.text = values.TryGetValue(row.DisplayName, out var value) ? value : "Lade...";
        }
    }
    private void BuildOverlay()
    {
        var canvasObject = new GameObject("Schalter Status Canvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        panelObject = CreatePanel("UAExpert Werte Panel", canvasObject.transform, new Color(0.94f, 0.97f, 0.95f, 0.98f)).gameObject;
        var panelRect = panelObject.GetComponent<RectTransform>();
        Stretch(panelRect, new Vector2(0f, 0.70f), Vector2.one, Vector2.zero, Vector2.zero);

        var titleBar = CreatePanel("Titel Leiste", panelObject.transform, new Color(0.10f, 0.16f, 0.18f, 0.96f));
        Stretch(titleBar.rectTransform, new Vector2(0f, 0.84f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        CreateText("Titel", titleBar.transform, "Industriesteuerung - UAExpert Werte", 22, FontStyle.Bold, Color.white,
            TextAnchor.MiddleLeft, new Vector2(0.02f, 0f), new Vector2(0.46f, 1f));
        CreateText("Hinweis", titleBar.transform, "Statuswerte werden beim Szenenstart geladen", 15, FontStyle.Normal, new Color(0.80f, 0.87f, 0.88f, 1f),
            TextAnchor.MiddleRight, new Vector2(0.50f, 0f), new Vector2(0.98f, 1f));

        var header = CreatePanel("Tabellen Kopf", panelObject.transform, new Color(0.78f, 0.84f, 0.80f, 1f));
        Stretch(header.rectTransform, new Vector2(0.015f, 0.74f), new Vector2(0.985f, 0.84f), Vector2.zero, Vector2.zero);
        CreateCell("Node Path Kopf", header.transform, "NODE PATH", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.015f, 0f), new Vector2(0.43f, 1f));
        CreateCell("Display Name Kopf", header.transform, "DISPLAY NAME", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.44f, 0f), new Vector2(0.63f, 1f));
        CreateCell("Value Kopf", header.transform, "VALUE", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.64f, 0f), new Vector2(0.985f, 1f));

        var body = new GameObject("Tabellen Inhalt");
        body.transform.SetParent(panelObject.transform, false);
        var bodyRect = body.AddComponent<RectTransform>();
        Stretch(bodyRect, new Vector2(0.015f, 0.04f), new Vector2(0.985f, 0.74f), Vector2.zero, Vector2.zero);

        rows.Clear();
        for (var i = 0; i < nodes.Length; i++)
        {
            AddRow(bodyRect, i, nodes[i]);
        }
    }

    private void AddRow(RectTransform body, int index, StatusNode node)
    {
        var yMax = 1f - index / (float)nodes.Length;
        var yMin = 1f - (index + 1) / (float)nodes.Length;
        var color = index % 2 == 0 ? new Color(0.88f, 0.94f, 0.90f, 1f) : new Color(0.98f, 0.99f, 0.98f, 1f);
        var row = CreatePanel("Status Zeile " + index, body, color);
        Stretch(row.rectTransform, new Vector2(0f, yMin), new Vector2(1f, yMax), Vector2.zero, Vector2.zero);

        CreateCell("Node Path", row.transform, node.NodePath, 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0.015f, 0f), new Vector2(0.43f, 1f));
        CreateCell("Display Name", row.transform, node.DisplayName, 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0.44f, 0f), new Vector2(0.63f, 1f));
        var valueText = CreateCell("Value", row.transform, "Lade...", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.64f, 0f), new Vector2(0.985f, 1f));
        rows.Add(new RowBinding(node.DisplayName, valueText));
    }

    private Image CreatePanel(string name, Transform parent, Color color)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        panel.AddComponent<RectTransform>();
        var image = panel.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateCell(string name, Transform parent, string value, int size, FontStyle style, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
    {
        var text = CreateText(name, parent, value, size, style, new Color(0.10f, 0.13f, 0.15f, 1f), anchor, anchorMin, anchorMax);
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        var rect = textObject.AddComponent<RectTransform>();
        Stretch(rect, anchorMin, anchorMax, new Vector2(6f, 0f), new Vector2(-6f, 0f));

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

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static string FormatOpcValue(object value)
    {
        if (value == null)
        {
            return "-";
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (value is Array array)
        {
            var items = new List<string>();
            foreach (var item in array)
            {
                items.Add(item != null ? item.ToString() : "-");
            }

            return string.Join(", ", items);
        }

        return value.ToString();
    }

    private static string ShortMessage(Exception exception)
    {
        if (exception is TypeInitializationException typeInitializationException && typeInitializationException.InnerException != null)
        {
            exception = typeInitializationException.InnerException;
        }

        return exception.GetType().Name + ": " + exception.Message;
    }

    private readonly struct StatusNode
    {
        public StatusNode(string nodePath, string displayName, string nodeId)
        {
            NodePath = nodePath;
            DisplayName = displayName;
            NodeId = nodeId;
        }

        public string NodePath { get; }
        public string DisplayName { get; }
        public string NodeId { get; }
    }

    private readonly struct RowBinding
    {
        public RowBinding(string displayName, Text valueText)
        {
            DisplayName = displayName;
            ValueText = valueText;
        }

        public string DisplayName { get; }
        public Text ValueText { get; }
    }
}
