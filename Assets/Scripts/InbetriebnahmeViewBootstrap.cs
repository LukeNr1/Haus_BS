using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class InbetriebnahmeViewBootstrap : MonoBehaviour
{
    private const string PdfFolderName = "InbetriebnahmePDFs";
    private const string ControllerAddress = "192.168.1.61";
    private const int ControllerPort = 4840;
    private const string TabletAddress = "192.168.1.193";
    private const float CheckViewInterval = 5f;
    private const string OpcUaEndpointUrl = "opc.tcp://192.168.1.61:4840";
    private const string StartSceneName = "StartFenster";
    private const string RoomSceneName = "Schalter";
    private const string SmartHomeSceneName = "SmartHome";

    private readonly List<DocumentEntry> documents = new()
    {
        new DocumentEntry(
            "Allgemeine Anleitung",
            "allgemeine_anleitung.pdf",
            "Allgemeine Anleitung",
            "Diese Anleitung begleitet die Inbetriebnahme des Systems vom Start bis zur Verbindungspruefung.",
            "1. Anwendung starten und dieses Startfenster pruefen.\n\n2. Passendes PDF in der linken Liste auswaehlen.\n\n3. Windows-System, VR-Brillen-System und Tablet-System nach den jeweiligen Beschreibungen vorbereiten.\n\n4. Industriesteuerung einschalten und Netzwerkverbindung herstellen.\n\n5. Mit \"Verbindung testen\" pruefen, ob die Steuerung erreichbar ist.\n\n6. Bei negativer Pruefung wird automatisch die Fehlerbehebung geoeffnet."),
        new DocumentEntry(
            "Windows-System",
            "windows_system.pdf",
            "Beschreibung Windows-System",
            "Das Windows-System dient als Hauptarbeitsplatz fuer Bedienung, Diagnose und Verbindungstest.",
            "Aufgaben:\n- Anwendung starten und PDF-Dokumente anzeigen.\n- WLAN oder LAN-Verbindung zur Industriesteuerung herstellen.\n- Verbindungstest ausfuehren.\n- Fehlerstatus anzeigen und passende Anleitung oeffnen.\n\nVorbereitung:\n- Netzwerkkarte aktivieren.\n- Mit dem vorgesehenen Anlagen-WLAN verbinden.\n- Firewall-Regeln fuer die Anwendung pruefen, falls keine Antwort kommt."),
        new DocumentEntry(
            "VR-Brillen-System",
            "vr_brillen_system.pdf",
            "Beschreibung VR-Brillen-System",
            "Das VR-Brillen-System zeigt die Inbetriebnahmeinformationen in einer immersiven Bedienumgebung.",
            "Aufgaben:\n- VR-Brille starten und ausreichend laden.\n- Mit demselben Netzwerk wie die Industriesteuerung verbinden.\n- Anwendung oeffnen und Systemstatus kontrollieren.\n- Bedienhinweise im Sichtfeld beachten.\n\nHinweise:\n- Trackingbereich freihalten.\n- Controller koppeln.\n- Bei Verbindungsproblemen WLAN kurz deaktivieren und erneut verbinden."),
        new DocumentEntry(
            "Tablet-System",
            "tablet_system.pdf",
            "Beschreibung Tablet-System",
            "Das Tablet-System ist fuer mobile Bedienung, kurze Kontrollen und schnelle Diagnose vorgesehen.",
            "Aufgaben:\n- Tablet einschalten und WLAN verbinden.\n- Anwendung starten.\n- Passende Dokumentation oeffnen.\n- Verbindung zur Industriesteuerung pruefen.\n\nHinweise:\n- Energiesparmodus waehrend der Inbetriebnahme vermeiden.\n- Displayhelligkeit fuer Lesbarkeit einstellen.\n- Bei schwacher Verbindung naeher an den Access Point gehen."),
        new DocumentEntry(
            "Fehlerbehebung",
            "fehlerbehebung.pdf",
            "Anleitung zur Fehlerbehebung",
            "Diese Anleitung wird automatisch geoeffnet, wenn der Verbindungstest negativ verlaeuft.",
            "Moegliche Schritte:\n- Ping erneut versuchen.\n- WLAN aus- und wieder einschalten.\n- Pruefen, ob Windows, VR-Brille oder Tablet im richtigen Netzwerk sind.\n- Industriesteuerung neu starten, wenn sie nicht erreichbar ist.\n- IP-Adresse und Subnetzmaske kontrollieren.\n- Firewall oder VPN kurz pruefen.\n- Kabel, Access Point und Spannungsversorgung kontrollieren."),
        new DocumentEntry(
            "Industriesteuerung",
            "industriesteuerung.pdf",
            "Beschreibung Industriesteuerung",
            "Allgemeine Daten und Rolle der Industriesteuerung im Inbetriebnahmeprozess.",
            "Allgemeine Daten:\n- Funktion: Zentrale Steuerung der Anlage.\n- Netzwerk: Erreichbar ueber die konfigurierte Anlagen-IP.\n- Standard-Testadresse in dieser View: 192.168.1.61.\n- Verbindungstest: ICMP Ping.\n\nBetrieb:\n- Steuerung muss eingeschaltet und hochgefahren sein.\n- Netzwerkstatus am Geraet oder Schaltschrank pruefen.\n- Bei Stoerung zuerst Versorgung, Netzwerk und IP-Konfiguration kontrollieren.")
    };

    private Font font;
    private Text titleText;
    private Text subtitleText;
    private Text bodyText;
    private Text statusText;
    private Text roomHintText;
    private GameObject startCanvasObject;
    private GameObject sceneSelectionObject;
    private GameObject roomRoot;
    private GameObject infoPanelObject;
    private GameObject checkViewOverlayObject;
    private RectTransform infoTableBody;
    private readonly List<ControllerInfoRow> controllerInfoRows = new();
    private Button openPdfButton;
    private Image checkViewOpcDot;
    private Text checkViewOpcText;
    private Image checkViewTabletDot;
    private Text checkViewTabletText;
    private readonly List<Button> documentButtons = new();
    private int selectedIndex;
    private bool roomReady;
    private bool isClosing;
    private bool checkViewOpcConnected;
    private bool checkViewTabletReachable;
    private Coroutine checkViewRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStartup()
    {
        if (SceneManager.GetActiveScene().name != StartSceneName)
        {
            return;
        }

        if (FindAnyObjectByType<InbetriebnahmeViewBootstrap>() != null)
        {
            return;
        }

        var host = new GameObject("Inbetriebnahme View Bootstrap");
        DontDestroyOnLoad(host);
        host.AddComponent<InbetriebnahmeViewBootstrap>();
    }

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        EnsureEventSystem();
        BuildView();
        SelectDocument(0);
    }

    private void Start()
    {
        checkViewRoutine = StartCoroutine(CheckViewLoop());
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

    private void BuildView()
    {
        var canvasObject = new GameObject("Inbetriebnahme Startfenster");
        startCanvasObject = canvasObject;
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var overlay = CreatePanel("Overlay", canvasObject.transform, new Color(0.04f, 0.05f, 0.06f, 0.96f));
        Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var window = CreatePanel("Fenster", overlay.transform, new Color(0.93f, 0.95f, 0.96f, 1f));
        Stretch(window.rectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

        var header = CreatePanel("Kopfbereich", window.transform, new Color(0.12f, 0.16f, 0.20f, 1f));
        Stretch(header.rectTransform, new Vector2(0f, 0.86f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        CreateText("Titel", header.transform, "Inbetriebnahme View", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.03f, 0.2f), new Vector2(0.54f, 0.85f));
        CreateText("Untertitel", header.transform, "PDF-Dokumentation fuer Windows, VR, Tablet und Industriesteuerung", 16, FontStyle.Normal,
            new Color(0.82f, 0.88f, 0.92f, 1f), TextAnchor.MiddleLeft, new Vector2(0.03f, 0.02f), new Vector2(0.7f, 0.42f));

        var checkInfoButton = CreateIconButton("CheckView Info", header.transform, "i", new Color(0.15f, 0.39f, 0.92f, 1f),
            new Vector2(0.665f, 0.42f), new Vector2(0.705f, 0.82f));
        checkInfoButton.onClick.AddListener(ShowCheckViewPanel);

        var testButton = CreateButton("Verbindung testen", header.transform, "Verbindung testen", new Color(0.10f, 0.46f, 0.40f, 1f),
            new Vector2(0.72f, 0.42f), new Vector2(0.89f, 0.82f));
        testButton.onClick.AddListener(TestConnection);

        var closeButton = CreateButton("View Wechsel", header.transform, "View Wechsel", new Color(0.36f, 0.40f, 0.44f, 1f),
            new Vector2(0.91f, 0.42f), new Vector2(0.98f, 0.82f));
        closeButton.onClick.AddListener(ShowSceneSelection);

        statusText = CreateText("Status", header.transform, "Bereit fuer Verbindungstest", 15, FontStyle.Normal,
            new Color(0.86f, 0.90f, 0.94f, 1f), TextAnchor.MiddleRight, new Vector2(0.58f, 0.05f), new Vector2(0.98f, 0.34f));

        var navigation = CreatePanel("PDF Auswahl", window.transform, new Color(0.82f, 0.87f, 0.89f, 1f));
        Stretch(navigation.rectTransform, new Vector2(0f, 0f), new Vector2(0.28f, 0.86f), Vector2.zero, Vector2.zero);

        CreateText("Auswahl Label", navigation.transform, "PDF-Dokumente", 20, FontStyle.Bold, new Color(0.10f, 0.14f, 0.18f, 1f),
            TextAnchor.MiddleLeft, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.98f));

        for (var i = 0; i < documents.Count; i++)
        {
            var index = i;
            var yMax = 0.86f - i * 0.105f;
            var yMin = yMax - 0.08f;
            var button = CreateButton(documents[i].Name, navigation.transform, documents[i].Name, new Color(0.18f, 0.24f, 0.29f, 1f),
                new Vector2(0.08f, yMin), new Vector2(0.92f, yMax));
            button.onClick.AddListener(() => SelectDocument(index));
            documentButtons.Add(button);
        }

        openPdfButton = CreateButton("PDF Oeffnen", navigation.transform, "PDF extern oeffnen", new Color(0.75f, 0.36f, 0.16f, 1f),
            new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.13f));
        openPdfButton.onClick.AddListener(OpenSelectedPdf);

        var content = CreatePanel("PDF Viewer", window.transform, Color.white);
        Stretch(content.rectTransform, new Vector2(0.28f, 0f), new Vector2(1f, 0.86f), Vector2.zero, Vector2.zero);

        titleText = CreateText("PDF Titel", content.transform, "", 26, FontStyle.Bold, new Color(0.09f, 0.12f, 0.16f, 1f),
            TextAnchor.MiddleLeft, new Vector2(0.06f, 0.87f), new Vector2(0.94f, 0.97f));
        subtitleText = CreateText("PDF Kurzbeschreibung", content.transform, "", 16, FontStyle.Normal, new Color(0.33f, 0.39f, 0.44f, 1f),
            TextAnchor.UpperLeft, new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.87f));

        var scrollRoot = CreatePanel("Scroll View", content.transform, new Color(0.96f, 0.97f, 0.98f, 1f));
        Stretch(scrollRoot.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.73f), Vector2.zero, Vector2.zero);
        var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        var viewport = CreatePanel("Viewport", scrollRoot.transform, new Color(0.96f, 0.97f, 0.98f, 1f));
        Stretch(viewport.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var textObject = new GameObject("PDF Text");
        textObject.transform.SetParent(viewport.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.offsetMin = new Vector2(24f, -980f);
        textRect.offsetMax = new Vector2(-24f, -18f);

        bodyText = textObject.AddComponent<Text>();
        bodyText.font = font;
        bodyText.fontSize = 19;
        bodyText.color = new Color(0.10f, 0.13f, 0.16f, 1f);
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.lineSpacing = 1.15f;

        scrollRect.viewport = viewport.rectTransform;
        scrollRect.content = textRect;

        BuildSceneSelection(canvasObject.transform);
        BuildCheckViewOverlay(canvasObject.transform);
    }

    private void BuildCheckViewOverlay(Transform parent)
    {
        checkViewOverlayObject = new GameObject("CheckView Overlay");
        checkViewOverlayObject.transform.SetParent(parent, false);
        var root = checkViewOverlayObject.AddComponent<RectTransform>();
        Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var dimmer = CreatePanel("CheckView Hintergrund", checkViewOverlayObject.transform, new Color(0f, 0f, 0f, 0.65f));
        Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var panel = CreatePanel("CheckView Panel", checkViewOverlayObject.transform, new Color(0.07f, 0.09f, 0.14f, 0.94f));
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(590f, 505f);
        panelRect.anchoredPosition = Vector2.zero;

        var header = CreatePanel("CheckView Kopf", panel.transform, new Color(0.11f, 0.15f, 0.24f, 1f));
        Stretch(header.rectTransform, new Vector2(0f, 0.86f), Vector2.one, Vector2.zero, Vector2.zero);
        CreateText("CheckView Titel", header.transform, "Verbindungsstatus", 24, FontStyle.Bold, Color.white,
            TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

        var divider = CreatePanel("CheckView Linie", header.transform, new Color(0.15f, 0.39f, 0.92f, 0.8f));
        Stretch(divider.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.04f), Vector2.zero, Vector2.zero);

        BuildCheckStatusRow(panel.transform, "Steuerung (OPC UA)", ControllerAddress + ":" + ControllerPort, 0.71f,
            new Color(0.12f, 0.15f, 0.23f, 1f), out checkViewOpcDot, out checkViewOpcText);
        BuildCheckStatusRow(panel.transform, "Tablet (Bewertung)", TabletAddress, 0.55f,
            new Color(0.14f, 0.17f, 0.26f, 1f), out checkViewTabletDot, out checkViewTabletText);

        Image windowsDot;
        Text windowsText;
        BuildCheckStatusRow(panel.transform, "Windows App", "Lokal", 0.39f,
            new Color(0.12f, 0.15f, 0.23f, 1f), out windowsDot, out windowsText);
        SetCheckDot(windowsDot, windowsText, new Color(0.13f, 0.77f, 0.37f, 1f), "Aktiv");

        var pdfButton = CreateButton("CheckView PDF", panel.transform, "PDF extern oeffnen", new Color(0.15f, 0.39f, 0.92f, 1f),
            new Vector2(0.28f, 0.19f), new Vector2(0.72f, 0.29f));
        pdfButton.onClick.AddListener(OpenSelectedPdf);

        var closeButton = CreateButton("CheckView Schliessen", panel.transform, "Schliessen", new Color(0.28f, 0.34f, 0.41f, 1f),
            new Vector2(0.34f, 0.055f), new Vector2(0.66f, 0.155f));
        closeButton.onClick.AddListener(HideCheckViewPanel);

        SetCheckDot(checkViewOpcDot, checkViewOpcText, new Color(0.98f, 0.80f, 0.08f, 1f), "Pruefe...");
        SetCheckDot(checkViewTabletDot, checkViewTabletText, new Color(0.98f, 0.80f, 0.08f, 1f), "Warte...");
        checkViewOverlayObject.SetActive(false);
    }

    private void BuildCheckStatusRow(Transform parent, string title, string detail, float yCenter, Color color, out Image dot, out Text statusTextField)
    {
        var row = CreatePanel("CheckView Zeile " + title, parent, color);
        Stretch(row.rectTransform, new Vector2(0.06f, yCenter - 0.065f), new Vector2(0.94f, yCenter + 0.065f), Vector2.zero, Vector2.zero);

        var dotObject = CreatePanel("Status Punkt", row.transform, new Color(0.9f, 0.1f, 0.1f, 1f));
        var dotRect = dotObject.rectTransform;
        dotRect.anchorMin = new Vector2(0f, 0.5f);
        dotRect.anchorMax = new Vector2(0f, 0.5f);
        dotRect.pivot = new Vector2(0f, 0.5f);
        dotRect.sizeDelta = new Vector2(20f, 20f);
        dotRect.anchoredPosition = new Vector2(18f, 0f);
        dot = dotObject;

        CreateText("Titel", row.transform, title, 18, FontStyle.Bold, Color.white, TextAnchor.LowerLeft,
            new Vector2(0.11f, 0.43f), new Vector2(0.72f, 0.92f));
        CreateText("Detail", row.transform, detail, 13, FontStyle.Normal, new Color(0.48f, 0.64f, 0.83f, 1f), TextAnchor.UpperLeft,
            new Vector2(0.11f, 0.08f), new Vector2(0.72f, 0.48f));
        statusTextField = CreateText("Status", row.transform, "-", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleRight,
            new Vector2(0.70f, 0.12f), new Vector2(0.97f, 0.88f));
    }

    private IEnumerator CheckViewLoop()
    {
        SetCheckDot(checkViewOpcDot, checkViewOpcText, new Color(0.98f, 0.80f, 0.08f, 1f), "Pruefe...");
        SetCheckDot(checkViewTabletDot, checkViewTabletText, new Color(0.98f, 0.80f, 0.08f, 1f), "Warte auf Steuerung...");

        while (!isClosing)
        {
            var opcTask = Task.Run(() => CanOpenTcp(ControllerAddress, ControllerPort, TimeSpan.FromSeconds(2)));
            while (!opcTask.IsCompleted)
            {
                yield return null;
            }

            checkViewOpcConnected = opcTask.Status == TaskStatus.RanToCompletion && opcTask.Result;
            SetCheckDot(checkViewOpcDot, checkViewOpcText,
                checkViewOpcConnected ? new Color(0.13f, 0.77f, 0.37f, 1f) : new Color(0.94f, 0.27f, 0.27f, 1f),
                checkViewOpcConnected ? "Verbunden" : "Getrennt");

            if (checkViewOpcConnected)
            {
                var tabletTask = Task.Run(() => CanPing(TabletAddress, 1000));
                while (!tabletTask.IsCompleted)
                {
                    yield return null;
                }

                checkViewTabletReachable = tabletTask.Status == TaskStatus.RanToCompletion && tabletTask.Result;
                SetCheckDot(checkViewTabletDot, checkViewTabletText,
                    checkViewTabletReachable ? new Color(0.13f, 0.77f, 0.37f, 1f) : new Color(0.94f, 0.27f, 0.27f, 1f),
                    checkViewTabletReachable ? "Online" : "Offline");
            }
            else
            {
                checkViewTabletReachable = false;
                SetCheckDot(checkViewTabletDot, checkViewTabletText, new Color(0.98f, 0.80f, 0.08f, 1f), "Warte auf Steuerung...");
            }

            var elapsed = 0f;
            while (elapsed < CheckViewInterval && !isClosing)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    private void ShowCheckViewPanel()
    {
        if (checkViewOverlayObject == null)
        {
            return;
        }

        checkViewOverlayObject.SetActive(true);
    }

    private void HideCheckViewPanel()
    {
        if (checkViewOverlayObject == null)
        {
            return;
        }

        checkViewOverlayObject.SetActive(false);
    }

    private void SetCheckDot(Image dot, Text text, Color color, string label)
    {
        if (dot != null)
        {
            dot.color = color;
        }

        if (text != null)
        {
            text.text = label;
        }
    }

    private void BuildSceneSelection(Transform parent)
    {
        sceneSelectionObject = new GameObject("Szenenauswahl");
        sceneSelectionObject.transform.SetParent(parent, false);
        var root = sceneSelectionObject.AddComponent<RectTransform>();
        Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var dimmer = CreatePanel("Hintergrund", sceneSelectionObject.transform, new Color(0.02f, 0.03f, 0.035f, 0.72f));
        Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var dialog = CreatePanel("Dialog", sceneSelectionObject.transform, new Color(0.94f, 0.96f, 0.96f, 1f));
        Stretch(dialog.rectTransform, new Vector2(0.33f, 0.34f), new Vector2(0.67f, 0.66f), Vector2.zero, Vector2.zero);

        CreateText("Titel", dialog.transform, "Szene auswaehlen", 25, FontStyle.Bold, new Color(0.08f, 0.12f, 0.15f, 1f),
            TextAnchor.MiddleLeft, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.90f));
        CreateText("Hinweis", dialog.transform, "Wohin willst du wechseln?", 16, FontStyle.Normal, new Color(0.28f, 0.34f, 0.38f, 1f),
            TextAnchor.UpperLeft, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.70f));

        var smartHomeButton = CreateButton("SmartHome Button", dialog.transform, "SmartHome", new Color(0.10f, 0.46f, 0.40f, 1f),
            new Vector2(0.08f, 0.33f), new Vector2(0.45f, 0.51f));
        smartHomeButton.onClick.AddListener(() => OpenScene(SmartHomeSceneName));

        var schalterButton = CreateButton("Schalter Button", dialog.transform, "Schalter", new Color(0.18f, 0.24f, 0.29f, 1f),
            new Vector2(0.55f, 0.33f), new Vector2(0.92f, 0.51f));
        schalterButton.onClick.AddListener(() => OpenScene(RoomSceneName));

        var cancelButton = CreateButton("Abbrechen Button", dialog.transform, "Abbrechen", new Color(0.46f, 0.49f, 0.50f, 1f),
            new Vector2(0.31f, 0.12f), new Vector2(0.69f, 0.27f));
        cancelButton.onClick.AddListener(HideSceneSelection);

        sceneSelectionObject.SetActive(false);
    }

    private void ShowSceneSelection()
    {
        if (sceneSelectionObject != null)
        {
            sceneSelectionObject.SetActive(true);
        }
    }

    private void HideSceneSelection()
    {
        if (sceneSelectionObject != null)
        {
            sceneSelectionObject.SetActive(false);
        }
    }

    private void EnterRoom()
    {
        if (!roomReady)
        {
            BuildRoom();
            BuildControllerInfoUi();
            roomReady = true;
        }

        if (startCanvasObject != null)
        {
            startCanvasObject.SetActive(false);
        }

        roomRoot.SetActive(true);
        infoPanelObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void BuildRoom()
    {
        DisableExistingCameras();

        roomRoot = new GameObject("Inbetriebnahme Raum");
        roomRoot.transform.SetParent(transform, false);

        CreateBox("Boden", roomRoot.transform, new Vector3(0f, -0.05f, 0f), new Vector3(11f, 0.1f, 11f), new Color(0.56f, 0.58f, 0.55f, 1f));
        CreateBox("Wand Nord", roomRoot.transform, new Vector3(0f, 2f, 5f), new Vector3(10f, 4f, 0.18f), new Color(0.78f, 0.80f, 0.78f, 1f));
        CreateBox("Wand Sued", roomRoot.transform, new Vector3(0f, 2f, -5f), new Vector3(10f, 4f, 0.18f), new Color(0.82f, 0.83f, 0.80f, 1f));
        CreateBox("Wand West", roomRoot.transform, new Vector3(-5f, 2f, 0f), new Vector3(0.18f, 4f, 10f), new Color(0.74f, 0.78f, 0.80f, 1f));
        CreateBox("Wand Ost", roomRoot.transform, new Vector3(5f, 2f, 0f), new Vector3(0.18f, 4f, 10f), new Color(0.74f, 0.78f, 0.80f, 1f));

        var lightObject = new GameObject("Deckenlicht");
        lightObject.transform.SetParent(roomRoot.transform, false);
        lightObject.transform.position = new Vector3(0f, 3.6f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = 4f;
        light.range = 12f;

        var playerObject = new GameObject("Spieler");
        playerObject.transform.SetParent(roomRoot.transform, false);
        playerObject.transform.position = new Vector3(0f, 1.05f, -3.8f);
        var controller = playerObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.28f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        playerObject.AddComponent<SimpleWasdPlayer>();

        var cameraObject = new GameObject("Raum Kamera");
        cameraObject.transform.SetParent(playerObject.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();

        var table = CreateTable(roomRoot.transform, new Vector3(0f, 0f, 2.15f));

        var modelHost = new GameObject("Industriesteuerung");
        modelHost.transform.SetParent(table.transform, false);
        modelHost.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        modelHost.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var modelPrefab = Resources.Load<GameObject>("Models/Steuerung-Modell");
        if (modelPrefab != null)
        {
            var model = Instantiate(modelPrefab, modelHost.transform);
            model.name = "Steuerung-Modell GLB";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 2.25f;
        }
        else
        {
            var placeholder = CreateBox("Steuerung Placeholder", modelHost.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 1.6f, 0.5f), new Color(0.18f, 0.23f, 0.25f, 1f));
            CreateBox("Display Placeholder", placeholder.transform, new Vector3(0f, 0.18f, -0.53f), new Vector3(0.9f, 0.34f, 0.04f), new Color(0.08f, 0.55f, 0.42f, 1f));
        }

        AddClickableCollider(modelHost);
        var info = modelHost.AddComponent<ControllerInfoTarget>();
        info.Configure(ShowControllerInfo);
        modelHost.AddComponent<AutoRotateController>();
        roomRoot.SetActive(false);
    }

    private GameObject CreateTable(Transform parent, Vector3 position)
    {
        var table = new GameObject("Steuerung Tisch");
        table.transform.SetParent(parent, false);
        table.transform.localPosition = position;

        CreateBox("Tischplatte", table.transform, new Vector3(0f, 0.78f, 0f), new Vector3(3.2f, 0.16f, 1.55f), new Color(0.46f, 0.39f, 0.31f, 1f));
        CreateBox("Tischbein Vorne Links", table.transform, new Vector3(-1.35f, 0.36f, -0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
        CreateBox("Tischbein Vorne Rechts", table.transform, new Vector3(1.35f, 0.36f, -0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
        CreateBox("Tischbein Hinten Links", table.transform, new Vector3(-1.35f, 0.36f, 0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
        CreateBox("Tischbein Hinten Rechts", table.transform, new Vector3(1.35f, 0.36f, 0.58f), new Vector3(0.16f, 0.72f, 0.16f), new Color(0.22f, 0.24f, 0.25f, 1f));
        return table;
    }

    private static void DisableExistingCameras()
    {
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            camera.enabled = false;
        }

        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            listener.enabled = false;
        }
    }

    private void BuildControllerInfoUi()
    {
        var canvasObject = new GameObject("Steuerung Info Overlay");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var hint = CreatePanel("Hinweis", canvasObject.transform, new Color(0.05f, 0.06f, 0.07f, 0.72f));
        Stretch(hint.rectTransform, new Vector2(0.02f, 0.91f), new Vector2(0.48f, 0.98f), Vector2.zero, Vector2.zero);
        roomHintText = CreateText("Hinweis Text", hint.transform, "WASD bewegen, Maus bewegen zum Umschauen, Linksklick auf die Steuerung zeigt Infos, ESC Maus frei", 15, FontStyle.Normal,
            Color.white, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));

        infoPanelObject = CreatePanel("Steuerung Popup", canvasObject.transform, new Color(0.95f, 0.97f, 0.96f, 0.96f)).gameObject;
        Stretch(infoPanelObject.GetComponent<RectTransform>(), new Vector2(0.56f, 0.16f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);

        CreateText("Popup Titel", infoPanelObject.transform, "Industriesteuerung - UAExpert Werte", 22, FontStyle.Bold,
            new Color(0.08f, 0.14f, 0.16f, 1f), TextAnchor.MiddleLeft, new Vector2(0.06f, 0.88f), new Vector2(0.88f, 0.98f));

        var close = CreateButton("Popup Schliessen", infoPanelObject.transform, "X", new Color(0.36f, 0.40f, 0.44f, 1f),
            new Vector2(0.90f, 0.90f), new Vector2(0.97f, 0.98f));
        close.onClick.AddListener(() => infoPanelObject.SetActive(false));

        var header = CreatePanel("Tabellen Kopf", infoPanelObject.transform, new Color(0.78f, 0.84f, 0.80f, 1f));
        Stretch(header.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.87f), Vector2.zero, Vector2.zero);
        CreateTableCell("Node Path Kopf", header.transform, "NODE PATH", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.02f, 0f), new Vector2(0.46f, 1f));
        CreateTableCell("Display Name Kopf", header.transform, "DISPLAY NAME", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.47f, 0f), new Vector2(0.67f, 1f));
        CreateTableCell("Value Kopf", header.transform, "VALUE", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.68f, 0f), new Vector2(0.98f, 1f));

        var body = new GameObject("Tabellen Inhalt");
        body.transform.SetParent(infoPanelObject.transform, false);
        infoTableBody = body.AddComponent<RectTransform>();
        Stretch(infoTableBody, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);

        CreateInfoRows();
        infoPanelObject.SetActive(false);
    }

    private async void ShowControllerInfo()
    {
        if (infoPanelObject == null)
        {
            return;
        }

        infoPanelObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetInfoValues(CreateLoadingValues("Verbinde mit " + OpcUaEndpointUrl + " ..."));

        try
        {
            var values = await ReadControllerInfoFromOpcUa();
            SetInfoValues(values);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("OPC-UA-Werte konnten nicht gelesen werden: " + exception.Message);
            SetInfoValues(CreateLoadingValues("OPC-UA nicht erreichbar: " + exception.Message));
        }
    }

    private void CreateInfoRows()
    {
        controllerInfoRows.Clear();
        AddInfoRow(0, "ServerDiagnostics/.../CurrentSessionCount", "CurrentSession...", "Lade...");
        AddInfoRow(1, "Server/ServerStatus/CurrentTime", "CurrentTime", "");
        AddInfoRow(2, "Server/ServerStatus/State", "State", "Lade...");
        AddInfoRow(3, "ServerStatus/BuildInfo/SoftwareVersion", "SoftwareVersion", "Lade...");
        AddInfoRow(4, "ServerStatus/BuildInfo/ManufacturerName", "ManufacturerN...", "Lade...");
        AddInfoRow(5, "ServerStatus/BuildInfo/BuildDate", "BuildDate", "Lade...");
        AddInfoRow(6, "Server/ServerArray", "ServerArray", "Lade...");
        AddInfoRow(7, "Resources/CPU/Model", "Model", "Lade...");
        AddInfoRow(8, "Resources/CPU/SerialNumber", "SerialNumber", "Lade...");
    }

    private void SetInfoValues(Dictionary<string, string> values)
    {
        foreach (var row in controllerInfoRows)
        {
            if (values.TryGetValue(row.ValueKey, out var value))
            {
                row.ValueText.text = value;
            }
        }
    }

    private static Dictionary<string, string> CreateLoadingValues(string message)
    {
        return new Dictionary<string, string>
        {
            ["CurrentSession..."] = message,
            ["CurrentTime"] = "Lade...",
            ["State"] = "Lade...",
            ["SoftwareVersion"] = "Lade...",
            ["ManufacturerN..."] = "Lade...",
            ["BuildDate"] = "Lade...",
            ["ServerArray"] = "Lade...",
            ["Model"] = "Lade...",
            ["SerialNumber"] = "Lade..."
        };
    }

    private async Task<Dictionary<string, string>> ReadControllerInfoFromOpcUa()
    {
        using var ping = new System.Net.NetworkInformation.Ping();
        var reply = await ping.SendPingAsync(ControllerAddress, 1500);
        var reachable = reply.Status == IPStatus.Success;

        return new Dictionary<string, string>
        {
            ["CurrentSession..."] = reachable ? "Steuerung erreichbar" : "Keine Antwort",
            ["CurrentTime"] = DateTime.Now.ToString("HH:mm:ss"),
            ["State"] = reachable ? "Ping OK" : reply.Status.ToString(),
            ["SoftwareVersion"] = "Nur in Schalter-Szene gelesen",
            ["ManufacturerN..."] = "B&R / X20",
            ["BuildDate"] = "-",
            ["ServerArray"] = OpcUaEndpointUrl,
            ["Model"] = "X20 Steuerung",
            ["SerialNumber"] = "Nicht ausgelesen"
        };
    }

    private static string FormatOpcValue(object value)
    {
        if (value == null)
        {
            return "-";
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
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

    private void AddInfoRow(int index, string nodePath, string displayName, string value)
    {
        var yMax = 1f - index / 9f;
        var yMin = 1f - (index + 1) / 9f;
        var background = CreatePanel("Info Zeile " + index, infoTableBody, index % 2 == 0 ? new Color(0.90f, 0.94f, 0.91f, 1f) : new Color(0.98f, 0.99f, 0.98f, 1f));
        Stretch(background.rectTransform, new Vector2(0f, yMin), new Vector2(1f, yMax), Vector2.zero, Vector2.zero);

        var nodeText = CreateTableCell("Node Path", background.transform, nodePath, 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0.02f, 0f), new Vector2(0.46f, 1f));
        var displayText = CreateTableCell("Display Name", background.transform, displayName, 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0.47f, 0f), new Vector2(0.67f, 1f));
        var valueText = CreateTableCell("Value", background.transform, value, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.68f, 0f), new Vector2(0.98f, 1f));
        controllerInfoRows.Add(new ControllerInfoRow(displayName, nodeText, displayText, valueText));
    }

    private Text CreateTableCell(string name, Transform parent, string value, int size, FontStyle style, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var text = CreateText(name, parent, value, size, style, new Color(0.10f, 0.13f, 0.15f, 1f), anchor, anchorMin, anchorMax);
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
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

    private static void AddClickableCollider(GameObject target)
    {
        var collider = target.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.55f, 0f);
        collider.size = new Vector3(2.7f, 2.2f, 1.8f);
    }

    private void SelectDocument(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, documents.Count - 1);
        var document = documents[selectedIndex];

        titleText.text = document.Title;
        subtitleText.text = document.Subtitle + "\nDatei: " + document.FileName + " - " + GetPdfLoadState(document);
        bodyText.text = document.Body;

        for (var i = 0; i < documentButtons.Count; i++)
        {
            var colors = documentButtons[i].colors;
            colors.normalColor = i == selectedIndex ? new Color(0.10f, 0.46f, 0.40f, 1f) : new Color(0.18f, 0.24f, 0.29f, 1f);
            colors.highlightedColor = i == selectedIndex ? new Color(0.12f, 0.54f, 0.47f, 1f) : new Color(0.25f, 0.32f, 0.38f, 1f);
            documentButtons[i].colors = colors;
        }

        openPdfButton.interactable = File.Exists(GetPdfPath(document));
    }

    private async void TestConnection()
    {
        if (!TrySetStatus("Teste Verbindung zu " + ControllerAddress + " ...", new Color(0.86f, 0.90f, 0.94f, 1f)))
        {
            return;
        }

        var reachable = false;
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(ControllerAddress, 1500);
            reachable = reply.Status == IPStatus.Success;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Verbindungstest fehlgeschlagen: " + exception.Message);
        }

        if (isClosing || this == null || statusText == null)
        {
            return;
        }

        if (reachable)
        {
            TrySetStatus("Verbindung erfolgreich: Industriesteuerung erreichbar.", new Color(0.45f, 0.95f, 0.70f, 1f));
            return;
        }

        TrySetStatus("Keine Verbindung. Fehlerbehebung wurde geoeffnet.", new Color(1f, 0.68f, 0.38f, 1f));
        SelectDocument(4);
    }

    private void OpenSelectedPdf()
    {
        var path = GetPdfPath(documents[selectedIndex]);
        if (!File.Exists(path))
        {
            statusText.text = "PDF nicht gefunden: " + documents[selectedIndex].FileName;
            statusText.color = new Color(1f, 0.55f, 0.45f, 1f);
            return;
        }

        Application.OpenURL(new Uri(path).AbsoluteUri);
    }

    private void OpenScene(string sceneName)
    {
        isClosing = true;
        LoadingScreenController.LoadScene(sceneName);
        Destroy(gameObject);
    }

    private bool TrySetStatus(string message, Color color)
    {
        if (isClosing || this == null || statusText == null)
        {
            return false;
        }

        statusText.text = message;
        statusText.color = color;
        return true;
    }

    private static bool CanOpenTcp(string address, int port, TimeSpan timeout)
    {
        try
        {
            using var tcpClient = new TcpClient();
            var asyncResult = tcpClient.BeginConnect(address, port, null, null);
            var connectedInTime = asyncResult.AsyncWaitHandle.WaitOne(timeout);
            if (!connectedInTime)
            {
                return false;
            }

            tcpClient.EndConnect(asyncResult);
            return tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanPing(string address, int timeoutMilliseconds)
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send(address, timeoutMilliseconds);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private string GetPdfLoadState(DocumentEntry document)
    {
        return File.Exists(GetPdfPath(document)) ? "PDF geladen" : "PDF fehlt";
    }

    private static string GetPdfPath(DocumentEntry document)
    {
        return Path.Combine(Application.streamingAssetsPath, PdfFolderName, document.FileName);
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

    private Button CreateIconButton(string name, Transform parent, string label, Color color, Vector2 anchorMin, Vector2 anchorMax)
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

        CreateText("Label", image.transform, label, 27, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(1f, 1f));

        return button;
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

    private readonly struct DocumentEntry
    {
        public DocumentEntry(string name, string fileName, string title, string subtitle, string body)
        {
            Name = name;
            FileName = fileName;
            Title = title;
            Subtitle = subtitle;
            Body = body;
        }

        public string Name { get; }
        public string FileName { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Body { get; }
    }

    private readonly struct ControllerInfoRow
    {
        public ControllerInfoRow(string valueKey, Text nodePathText, Text displayNameText, Text valueText)
        {
            ValueKey = valueKey;
            NodePathText = nodePathText;
            DisplayNameText = displayNameText;
            ValueText = valueText;
        }

        public string ValueKey { get; }
        public Text NodePathText { get; }
        public Text DisplayNameText { get; }
        public Text ValueText { get; }
    }
}

public sealed class SimpleWasdPlayer : MonoBehaviour
{
    private const float MouseSensitivity = 0.12f;
    private const float MoveSpeed = 3.2f;
    private const float KeyboardTurnSpeed = 120f;
    private const float Gravity = -18f;

    private CharacterController controller;
    private Transform cameraTransform;
    private float pitch;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        var camera = GetComponentInChildren<Camera>();
        cameraTransform = camera != null ? camera.transform : transform;
    }

    private void Update()
    {
        HandleCursor();
        HandleLook();
        HandleMove();
    }

    private void HandleCursor()
    {
        if (WasPressedEscape())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (WasPressedInteractionMode())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        var pointerOverUi = eventSystem != null && eventSystem.IsPointerOverGameObject();
        if (WasPressedLeftMouse() && Cursor.lockState != CursorLockMode.Locked && !pointerOverUi)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        var delta = GetMouseDelta() * MouseSensitivity;
        transform.Rotate(Vector3.up * delta.x);
        transform.Rotate(Vector3.up * GetKeyboardTurnInput() * KeyboardTurnSpeed * Time.deltaTime);
        pitch = Mathf.Clamp(pitch - delta.y, -78f, 78f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMove()
    {
        var input = GetMovementInput();
        var movement = transform.right * input.x + transform.forward * input.y;
        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }

        verticalVelocity += Gravity * Time.deltaTime;
        var velocity = movement * MoveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private static Vector2 GetMovementInput()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        var x = 0f;
        var y = 0f;
        if (keyboard.aKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed) y += 1f;
        return new Vector2(x, y);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    private static Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }

    private static bool WasPressedLeftMouse()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool WasPressedEscape()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool WasPressedInteractionMode()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private static float GetKeyboardTurnInput()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        var value = 0f;
        if (keyboard.qKey.isPressed) value -= 1f;
        if (keyboard.rKey.isPressed) value += 1f;
        return value;
#else
        var value = 0f;
        if (Input.GetKey(KeyCode.Q)) value -= 1f;
        if (Input.GetKey(KeyCode.R)) value += 1f;
        return value;
#endif
    }
}

public sealed class ControllerInfoTarget : MonoBehaviour
{
    private Action showInfo;

    public void Configure(Action onShowInfo)
    {
        showInfo = onShowInfo;
    }

    private void Update()
    {
        if (!WasPressedLeftMouse() || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        var ray = new Ray(camera.transform.position, camera.transform.forward);
        if (Physics.Raycast(ray, out var hit, 4f) && (hit.transform == transform || hit.transform.IsChildOf(transform)))
        {
            showInfo?.Invoke();
        }
    }

    private static bool WasPressedLeftMouse()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}

public sealed class AutoRotateController : MonoBehaviour
{
    private const float RotationSpeed = 22f;

    private void Update()
    {
        transform.Rotate(Vector3.up, RotationSpeed * Time.deltaTime, Space.World);
    }
}
