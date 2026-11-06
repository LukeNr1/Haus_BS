using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class SmartHomeControlStation : MonoBehaviour
{
    private const string DefaultModelResourcePath = "Models/Steuerung-Modell";
    private const string DefaultControllerAddress = "192.168.1.61";
    private const string DefaultOpcUaEndpointUrl = "opc.tcp://192.168.1.61:4840";

    [SerializeField] private string modelResourcePath = DefaultModelResourcePath;
    [SerializeField] private string controllerAddress = DefaultControllerAddress;
    [SerializeField] private string opcUaEndpointUrl = DefaultOpcUaEndpointUrl;
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private Vector3 modelScale = Vector3.one * 2.25f;
    [SerializeField] private string placementPlaneName = "Plane";
    [SerializeField] private bool snapToPlacementPlane = true;

    private readonly List<StatusRow> statusRows = new();
    private Font font;
    private GameObject stationVisualRoot;
    private GameObject infoPanelObject;
    private RectTransform tableBody;
    private RectTransform closeButtonRect;
    private Transform modelHost;
    private bool isReadingStatus;
    private bool built;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        EnsureEventSystem();
        BuildStation();
        BuildStatusOverlay();
    }

    private void Update()
    {
        if (infoPanelObject != null && infoPanelObject.activeSelf)
        {
            if (WasClosePressed() || WasCloseButtonClicked())
            {
                CloseStatusPanel();
            }

            return;
        }

        if (WasPrimaryPressed() && !IsPointerOverUi() && HitStation())
        {
            ShowControllerStatus();
        }
    }

    private void BuildStation()
    {
        if (built)
        {
            return;
        }

        built = true;
        SnapToPlacementPlane();
        stationVisualRoot = new GameObject("Tisch mit Steuerung");
        stationVisualRoot.transform.SetParent(transform, false);

        CreateTable(stationVisualRoot.transform);
        CreateControlModel(stationVisualRoot.transform);
    }

    private void SnapToPlacementPlane()
    {
        if (!snapToPlacementPlane)
        {
            return;
        }

        var placementPlane = GameObject.Find(placementPlaneName);
        if (placementPlane == null)
        {
            return;
        }

        var planeRenderer = placementPlane.GetComponentInChildren<Renderer>();
        if (planeRenderer == null)
        {
            transform.position = placementPlane.transform.position;
            return;
        }

        var bounds = planeRenderer.bounds;
        transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    private void CreateTable(Transform parent)
    {
        var table = new GameObject("Steuerung Tisch");
        table.transform.SetParent(parent, false);

        CreateBox("Tischplatte", table.transform, new Vector3(0f, 0.78f, 0f), new Vector3(3.2f, 0.16f, 1.55f), new Color(0.46f, 0.39f, 0.31f, 1f));
        CreateBox("Tischbein Vorne Links", table.transform, new Vector3(-1.35f, 0.36f, -0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
        CreateBox("Tischbein Vorne Rechts", table.transform, new Vector3(1.35f, 0.36f, -0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
        CreateBox("Tischbein Hinten Links", table.transform, new Vector3(-1.35f, 0.36f, 0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
        CreateBox("Tischbein Hinten Rechts", table.transform, new Vector3(1.35f, 0.36f, 0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
    }

    private void CreateControlModel(Transform parent)
    {
        var hostObject = new GameObject("Industriesteuerung");
        hostObject.transform.SetParent(parent, false);
        hostObject.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        hostObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        modelHost = hostObject.transform;

        var modelPrefab = Resources.Load<GameObject>(modelResourcePath);
        if (modelPrefab != null)
        {
            var model = Instantiate(modelPrefab, modelHost);
            model.name = "Steuerung-Modell GLB";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = modelScale;
        }
        else
        {
            var placeholder = CreateBox("Steuerung Placeholder", modelHost, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 1.6f, 0.5f), new Color(0.18f, 0.23f, 0.25f, 1f));
            CreateBox("Display Placeholder", placeholder.transform, new Vector3(0f, 0.18f, -0.53f), new Vector3(0.9f, 0.34f, 0.04f), new Color(0.08f, 0.55f, 0.42f, 1f));
        }

        var collider = hostObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.55f, 0f);
        collider.size = new Vector3(2.7f, 2.2f, 1.8f);
    }

    private void BuildStatusOverlay()
    {
        var canvasObject = new GameObject("SmartHome Steuerung Status Overlay");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 560;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        infoPanelObject = CreatePanel("Statusanzeige", canvasObject.transform, new Color(0.95f, 0.97f, 0.96f, 0.97f)).gameObject;
        Stretch(infoPanelObject.GetComponent<RectTransform>(), new Vector2(0.55f, 0.15f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);

        CreateText("Titel", infoPanelObject.transform, "Industriesteuerung - Status", 22, FontStyle.Bold,
            new Color(0.08f, 0.14f, 0.16f, 1f), TextAnchor.MiddleLeft, new Vector2(0.06f, 0.88f), new Vector2(0.88f, 0.98f));

        var close = CreateButton("Schliessen", infoPanelObject.transform, "X", new Color(0.36f, 0.40f, 0.44f, 1f),
            new Vector2(0.90f, 0.90f), new Vector2(0.97f, 0.98f));
        closeButtonRect = close.GetComponent<RectTransform>();
        close.onClick.AddListener(CloseStatusPanel);

        var header = CreatePanel("Tabellen Kopf", infoPanelObject.transform, new Color(0.78f, 0.84f, 0.80f, 1f));
        Stretch(header.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.87f), Vector2.zero, Vector2.zero);
        CreateTableCell("Name Kopf", header.transform, "NAME", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.03f, 0f), new Vector2(0.42f, 1f));
        CreateTableCell("Wert Kopf", header.transform, "WERT", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.43f, 0f), new Vector2(0.97f, 1f));

        var body = new GameObject("Tabellen Inhalt");
        body.transform.SetParent(infoPanelObject.transform, false);
        tableBody = body.AddComponent<RectTransform>();
        Stretch(tableBody, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);

        CreateStatusRows();
        infoPanelObject.SetActive(false);
    }

    private void CloseStatusPanel()
    {
        if (infoPanelObject == null)
        {
            return;
        }

        infoPanelObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private async void ShowControllerStatus()
    {
        if (infoPanelObject == null || isReadingStatus)
        {
            return;
        }

        PressFeedback();
        infoPanelObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetStatusValues(CreateLoadingValues("Pruefe Verbindung ..."));

        isReadingStatus = true;
        try
        {
            var values = await ReadStatusValues();
            SetStatusValues(values);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Statusanzeige konnte die Steuerung nicht pruefen: " + exception.Message);
            SetStatusValues(CreateLoadingValues("Fehler: " + exception.Message));
        }
        finally
        {
            isReadingStatus = false;
        }
    }

    private async Task<Dictionary<string, string>> ReadStatusValues()
    {
        var pingStatus = IPStatus.Unknown;
        var reachable = false;

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(controllerAddress, 1500);
            pingStatus = reply.Status;
            reachable = reply.Status == IPStatus.Success;
        }
        catch
        {
            pingStatus = IPStatus.Unknown;
        }

        return new Dictionary<string, string>
        {
            ["Verbindung"] = reachable ? "Steuerung erreichbar" : "Keine Antwort",
            ["Adresse"] = controllerAddress,
            ["OPC-UA Endpoint"] = opcUaEndpointUrl,
            ["Ping Status"] = pingStatus.ToString(),
            ["Zeit"] = DateTime.Now.ToString("HH:mm:ss"),
            ["Modell"] = "B&R X20 Steuerung",
            ["Aktion"] = "Statusanzeige wurde durch Klick geoeffnet"
        };
    }

    private void CreateStatusRows()
    {
        statusRows.Clear();
        AddStatusRow(0, "Verbindung", "Bereit");
        AddStatusRow(1, "Adresse", controllerAddress);
        AddStatusRow(2, "OPC-UA Endpoint", opcUaEndpointUrl);
        AddStatusRow(3, "Ping Status", "-");
        AddStatusRow(4, "Zeit", "-");
        AddStatusRow(5, "Modell", "B&R X20 Steuerung");
        AddStatusRow(6, "Aktion", "-");
    }

    private void SetStatusValues(Dictionary<string, string> values)
    {
        foreach (var row in statusRows)
        {
            if (values.TryGetValue(row.Key, out var value))
            {
                row.ValueText.text = value;
            }
        }
    }

    private static Dictionary<string, string> CreateLoadingValues(string message)
    {
        return new Dictionary<string, string>
        {
            ["Verbindung"] = message,
            ["Adresse"] = DefaultControllerAddress,
            ["OPC-UA Endpoint"] = DefaultOpcUaEndpointUrl,
            ["Ping Status"] = "Lade...",
            ["Zeit"] = DateTime.Now.ToString("HH:mm:ss"),
            ["Modell"] = "B&R X20 Steuerung",
            ["Aktion"] = "Klick erkannt"
        };
    }

    private void AddStatusRow(int index, string label, string value)
    {
        var rowCount = 7f;
        var yMax = 1f - index / rowCount;
        var yMin = 1f - (index + 1) / rowCount;
        var background = CreatePanel("Status Zeile " + index, tableBody, index % 2 == 0 ? new Color(0.90f, 0.94f, 0.91f, 1f) : new Color(0.98f, 0.99f, 0.98f, 1f));
        Stretch(background.rectTransform, new Vector2(0f, yMin), new Vector2(1f, yMax), Vector2.zero, Vector2.zero);

        CreateTableCell("Name", background.transform, label, 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0.03f, 0f), new Vector2(0.42f, 1f));
        var valueText = CreateTableCell("Wert", background.transform, value, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.43f, 0f), new Vector2(0.97f, 1f));
        statusRows.Add(new StatusRow(label, valueText));
    }

    private void PressFeedback()
    {
        if (modelHost == null)
        {
            return;
        }

        StopAllCoroutines();
        StartCoroutine(PressFeedbackRoutine());
    }

    private System.Collections.IEnumerator PressFeedbackRoutine()
    {
        var start = modelHost.localPosition;
        modelHost.localPosition = start + Vector3.down * 0.035f;
        yield return new WaitForSeconds(0.08f);
        if (modelHost != null)
        {
            modelHost.localPosition = start;
        }
    }

    private bool HitStation()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        Ray ray;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = new Ray(camera.transform.position, camera.transform.forward);
        }
        else
        {
            ray = camera.ScreenPointToRay(GetPointerPosition());
        }

        return modelHost != null
            && Physics.Raycast(ray, out var hit, interactionDistance)
            && (hit.transform == modelHost || hit.transform.IsChildOf(modelHost));
    }

    private static bool IsPointerOverUi()
    {
        var eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }

    private static Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    private bool WasCloseButtonClicked()
    {
        return WasPrimaryPressed()
            && closeButtonRect != null
            && RectTransformUtility.RectangleContainsScreenPoint(closeButtonRect, GetPointerPosition());
    }

    private static bool WasClosePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private static bool WasPrimaryPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = null;
        var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        foreach (var candidate in eventSystems)
        {
            if (eventSystem == null && candidate.isActiveAndEnabled)
            {
                eventSystem = candidate;
            }
            else
            {
                candidate.enabled = false;
            }
        }

        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
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

    private GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;

        var renderer = box.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        renderer.material = new Material(shader);
        renderer.material.color = color;
        return box;
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

    private Text CreateTableCell(string name, Transform parent, string value, int size, FontStyle style, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var text = CreateText(name, parent, value, size, style, new Color(0.10f, 0.13f, 0.15f, 1f), anchor, anchorMin, anchorMax);
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

        CreateText("Label", image.transform, label, 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter,
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

    private readonly struct StatusRow
    {
        public StatusRow(string key, Text valueText)
        {
            Key = key;
            ValueText = valueText;
        }

        public string Key { get; }
        public Text ValueText { get; }
    }
}
