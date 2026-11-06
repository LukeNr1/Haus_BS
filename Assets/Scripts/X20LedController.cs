using System;
using System.Collections.Generic;
using UnityEngine;

public class X20LedController : MonoBehaviour
{
    [Serializable]
    public class RoomLedBinding
    {
        public string roomName = "room1";
        public Renderer inputLedRenderer;
        public Renderer outputLedRenderer;
        public string inputLedObjectName = "Head_LED_0_1";
        public string outputLedObjectName = "Module_1_PlateLED";
        public int inputModuleNumber = 1;
        public int outputModuleNumber = 4;
        public Color inputOnColor = new Color(0.2f, 1f, 0.2f);
        public Color outputOnColor = new Color(1f, 0.75f, 0.15f);
    }

    private class LedState
    {
        public Renderer Renderer;
        public Material Material;
        public Color OriginalBaseColor;
        public Color OriginalEmissionColor;
        public bool HadEmissionKeyword;
    }

    private class VisualLed
    {
        public Renderer Renderer;
        public Light Light;
        public Material Material;
        public Color OnColor;
    }

    public List<RoomLedBinding> bindings = new List<RoomLedBinding>
    {
        new RoomLedBinding
        {
            roomName = "room1",
            inputLedObjectName = "Module_1_PlateLED",
            outputLedObjectName = "Module_4_PlateLED",
            inputModuleNumber = 1,
            outputModuleNumber = 4
        },
        new RoomLedBinding
        {
            roomName = "room2",
            inputLedObjectName = "Module_2_PlateLED",
            outputLedObjectName = "Module_5_PlateLED",
            inputModuleNumber = 2,
            outputModuleNumber = 5
        },
        new RoomLedBinding
        {
            roomName = "room3",
            inputLedObjectName = "Module_3_PlateLED",
            outputLedObjectName = "Module_6_PlateLED",
            inputModuleNumber = 3,
            outputModuleNumber = 6
        }
    };

    [Range(0.1f, 10f)]
    public float emissionIntensity = 3f;

    [Range(0f, 1f)]
    public float offBrightness = 0.08f;

    public bool createVisibleLedMarkers = true;
    public float markerLocalSize = 0.004f;
    public float markerLightIntensity = 1.5f;
    public float markerLightRange = 0.35f;

    private static X20LedController instance;
    private readonly Dictionary<Renderer, LedState> ledStates = new Dictionary<Renderer, LedState>();
    private readonly Dictionary<string, VisualLed> visibleLeds = new Dictionary<string, VisualLed>();

    public static X20LedController Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<X20LedController>();

            return instance;
        }
    }

    public static X20LedController GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var controllerObject = new GameObject("X20 LED Controller");
        instance = controllerObject.AddComponent<X20LedController>();
        DontDestroyOnLoad(controllerObject);
        return instance;
    }

    void Awake()
    {
        EnsureDefaultBindings();

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        EnsureDefaultBindings();
        ResolveAllBindings();
        EnsureVisibleLedMarkers();
        SetAllLeds(false);
    }

    public void SetInputLed(string roomName, bool isOn)
    {
        SetLed(roomName, true, isOn);
    }

    public void SetOutputLed(string roomName, bool isOn)
    {
        SetLed(roomName, false, isOn);
    }

    public void SetAllLeds(bool isOn)
    {
        EnsureVisibleLedMarkers();

        foreach (var binding in bindings)
        {
            SetLed(binding, binding.inputLedRenderer, binding.inputOnColor, isOn);
            SetLed(binding, binding.outputLedRenderer, binding.outputOnColor, isOn);
            SetVisibleLed(binding, true, isOn);
            SetVisibleLed(binding, false, isOn);
        }
    }

    private void SetLed(string roomName, bool isInput, bool isOn)
    {
        EnsureDefaultBindings();
        ResolveAllBindings();
        EnsureVisibleLedMarkers();

        var binding = bindings.Find(item => string.Equals(item.roomName, roomName, StringComparison.OrdinalIgnoreCase));
        if (binding == null)
        {
            Debug.LogWarning("X20 LED: Kein Binding fuer Raum gefunden: " + roomName);
            return;
        }

        var renderer = isInput ? binding.inputLedRenderer : binding.outputLedRenderer;
        var color = isInput ? binding.inputOnColor : binding.outputOnColor;
        SetLed(binding, renderer, color, isOn);
        SetVisibleLed(binding, isInput, isOn);
    }

    private void ResolveAllBindings()
    {
        foreach (var binding in bindings)
        {
            if (binding.inputLedRenderer == null)
                binding.inputLedRenderer = FindRendererByName(binding.inputLedObjectName);

            if (binding.outputLedRenderer == null)
                binding.outputLedRenderer = FindRendererByName(binding.outputLedObjectName);
        }
    }

    private Renderer FindRendererByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Renderer partialMatch = null;
        foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if (renderer == null || renderer.gameObject == null)
                continue;

            if (!renderer.gameObject.scene.IsValid())
                continue;

            if (renderer.gameObject.name == objectName)
                return renderer;

            if (partialMatch == null && renderer.gameObject.name.StartsWith(objectName, StringComparison.Ordinal))
                partialMatch = renderer;
        }

        return partialMatch;
    }

    private void EnsureDefaultBindings()
    {
        if (bindings != null && bindings.Count > 0)
            return;

        bindings = new List<RoomLedBinding>
        {
            new RoomLedBinding
            {
                roomName = "room1",
                inputLedObjectName = "Module_1_PlateLED",
                outputLedObjectName = "Module_4_PlateLED",
                inputModuleNumber = 1,
                outputModuleNumber = 4
            },
            new RoomLedBinding
            {
                roomName = "room2",
                inputLedObjectName = "Module_2_PlateLED",
                outputLedObjectName = "Module_5_PlateLED",
                inputModuleNumber = 2,
                outputModuleNumber = 5
            },
            new RoomLedBinding
            {
                roomName = "room3",
                inputLedObjectName = "Module_3_PlateLED",
                outputLedObjectName = "Module_6_PlateLED",
                inputModuleNumber = 3,
                outputModuleNumber = 6
            }
        };
    }

    private void EnsureVisibleLedMarkers()
    {
        if (!createVisibleLedMarkers)
            return;

        var anchor = FindX20Root();
        if (anchor == null)
            return;

        foreach (var binding in bindings)
        {
            NormalizeModuleNumbers(binding);
            CreateVisibleLed(anchor, binding.roomName + "_input", binding.inputModuleNumber, binding.inputOnColor);
            CreateVisibleLed(anchor, binding.roomName + "_output", binding.outputModuleNumber, binding.outputOnColor);
        }
    }

    private Transform FindX20Root()
    {
        var root = GameObject.Find("X20_ROOT");
        if (root != null)
            return root.transform;

        var model = GameObject.Find("Steuerung-Modell");
        return model != null ? model.transform : null;
    }

    private void CreateVisibleLed(Transform anchor, string key, int moduleNumber, Color onColor)
    {
        if (moduleNumber <= 0 || visibleLeds.ContainsKey(key))
            return;

        var existing = anchor.Find("LED_" + key);
        var ledObject = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ledObject.name = "LED_" + key;
        ledObject.transform.SetParent(anchor, false);
        ledObject.transform.localPosition = GetModuleLedLocalPosition(moduleNumber);
        ledObject.transform.localRotation = Quaternion.identity;
        ledObject.transform.localScale = Vector3.one * markerLocalSize;

        var collider = ledObject.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);

        var renderer = ledObject.GetComponent<Renderer>();
        renderer.material = material;

        var light = ledObject.GetComponent<Light>();
        if (light == null)
            light = ledObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = markerLightRange;
        light.intensity = 0f;
        light.color = onColor;

        visibleLeds.Add(key, new VisualLed
        {
            Renderer = renderer,
            Light = light,
            Material = material,
            OnColor = onColor
        });

        SetVisibleLed(key, false);
    }

    private Vector3 GetModuleLedLocalPosition(int moduleNumber)
    {
        const float module1X = -0.034174286f;
        const float moduleSpacing = 0.014714286f;
        var x = module1X + (moduleNumber - 1) * moduleSpacing;
        return new Vector3(x, 0.0615f, 0.043f);
    }

    private void NormalizeModuleNumbers(RoomLedBinding binding)
    {
        if (binding.inputModuleNumber <= 0)
            binding.inputModuleNumber = ExtractModuleNumber(binding.inputLedObjectName, 1);

        if (binding.outputModuleNumber <= 0)
            binding.outputModuleNumber = ExtractModuleNumber(binding.outputLedObjectName, 4);
    }

    private int ExtractModuleNumber(string objectName, int fallback)
    {
        if (string.IsNullOrEmpty(objectName))
            return fallback;

        const string prefix = "Module_";
        var start = objectName.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
            return fallback;

        start += prefix.Length;
        var end = objectName.IndexOf('_', start);
        if (end < 0)
            return fallback;

        return int.TryParse(objectName.Substring(start, end - start), out var number)
            ? number
            : fallback;
    }

    private void SetVisibleLed(RoomLedBinding binding, bool isInput, bool isOn)
    {
        var key = binding.roomName + (isInput ? "_input" : "_output");
        SetVisibleLed(key, isOn);
    }

    private void SetVisibleLed(string key, bool isOn)
    {
        if (!visibleLeds.TryGetValue(key, out var led))
            return;

        var color = isOn ? led.OnColor : led.OnColor * 0.08f;
        color.a = 1f;

        SetColor(led.Material, "_BaseColor", color);
        SetColor(led.Material, "_Color", color);
        led.Material.EnableKeyword("_EMISSION");
        SetColor(led.Material, "_EmissionColor", isOn ? led.OnColor * emissionIntensity : Color.black);

        if (led.Light != null)
        {
            led.Light.enabled = isOn;
            led.Light.intensity = isOn ? markerLightIntensity : 0f;
        }
    }

    private void SetLed(RoomLedBinding binding, Renderer ledRenderer, Color onColor, bool isOn)
    {
        if (ledRenderer == null)
            return;

        var state = GetState(ledRenderer);
        var targetBaseColor = isOn ? onColor : state.OriginalBaseColor * offBrightness;
        targetBaseColor.a = state.OriginalBaseColor.a;

        SetColor(state.Material, "_BaseColor", targetBaseColor);
        SetColor(state.Material, "_Color", targetBaseColor);

        if (isOn)
        {
            state.Material.EnableKeyword("_EMISSION");
            SetColor(state.Material, "_EmissionColor", onColor * emissionIntensity);
        }
        else
        {
            if (!state.HadEmissionKeyword)
                state.Material.DisableKeyword("_EMISSION");

            SetColor(state.Material, "_EmissionColor", Color.black);
        }
    }

    private LedState GetState(Renderer ledRenderer)
    {
        if (ledStates.TryGetValue(ledRenderer, out var state))
            return state;

        var material = ledRenderer.material;
        state = new LedState
        {
            Renderer = ledRenderer,
            Material = material,
            OriginalBaseColor = GetColor(material, "_BaseColor", GetColor(material, "_Color", Color.white)),
            OriginalEmissionColor = GetColor(material, "_EmissionColor", Color.black),
            HadEmissionKeyword = material.IsKeywordEnabled("_EMISSION")
        };

        ledStates.Add(ledRenderer, state);
        return state;
    }

    private static Color GetColor(Material material, string propertyName, Color fallback)
    {
        return material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
    }

    private static void SetColor(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }
}
