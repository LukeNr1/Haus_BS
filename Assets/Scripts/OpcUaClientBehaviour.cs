// Copyright (c) Traeger Industry Components GmbH. All Rights Reserved.
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Opc.UaFx;
using Opc.UaFx.Client;

public class OpcUaClientBehaviour : MonoBehaviour
{
    private OpcClient client;
    private Text statusText;
    private Text statusText4;
    private Text statusText3;
    private OpcSubscription subscription;

    public Light zielLicht;
    public string roomName = "room1";
    public bool updateX20Leds = true;
    public X20LedController x20LedController;

    private Vector3 startPosition;
    private Vector3 gedruecktPosition;
    private float klickOffset = 0.00077f;
    private bool istGedrueckt = false;
    private bool lichtAn = false;
    private bool isConnected = false;

    void Start()
    {
        if (updateX20Leds && x20LedController == null)
            x20LedController = X20LedController.GetOrCreate();

        if (zielLicht != null)
            zielLicht.enabled = false;

        SetInputLed(false);
        SetOutputLed(false);

        statusText = GameObject.Find("statusText").GetComponent<Text>();
        statusText4 = GameObject.Find("statusText4").GetComponent<Text>();
        statusText3 = GameObject.Find("statusText3").GetComponent<Text>();

        statusText.text = "Connecting...";
        statusText4.text = "Connecting4...";
        statusText3.text = "Info3...";

        startPosition = transform.localPosition;
        gedruecktPosition = startPosition - new Vector3(0f, klickOffset, 0f);
        transform.localPosition = startPosition;

        try
        {
            client = new OpcClient("opc.tcp://192.168.1.61:4840");
            client.Security.UserIdentity = new OpcClientIdentity("opcuser1", ".opcuser1");
            client.Connect();

            isConnected = true;
            statusText.text = "Connected!";

            string[] nodeIds =
            {
                "ns=6;s=::AsGlobalPV:gSchweibsChange",
                "ns=6;s=::AsGlobalPV:gSchweibsWrite",
                $"ns=6;s=::{roomName}:Lampe",
                $"ns=6;s=::{roomName}:SwitchValueW",
                $"ns=6;s=::{roomName}:SwitchValue"
            };

            subscription = client.SubscribeNodes();

            for (int index = 0; index < nodeIds.Length; index++)
            {
                var item = new OpcMonitoredItem(nodeIds[index], OpcAttribute.Value);
                item.DataChangeReceived += HandleDataChanged;
                item.Tag = index;
                item.SamplingInterval = 200;
                subscription.AddMonitoredItem(item);
            }

            subscription.ApplyChanges();
            statusText3.text = "Subscribed! Room: " + roomName;
        }
        catch (Exception ex)
        {
            if (ex is TypeInitializationException tiex && tiex.InnerException != null)
                ex = tiex.InnerException;

            statusText.text += Environment.NewLine
                + ex.GetType().Name + ": " + ex.Message
                + Environment.NewLine + ex.StackTrace;
        }
    }

    void Update()
    {
        if (isConnected && client != null && client.State != OpcClientState.Connected)
        {
            Debug.LogError($"OPC: Verbindung getrennt! (Room: {roomName}) - Zustand: {client.State}");
            if (statusText != null)
                statusText.text = "VERBINDUNG GETRENNT!";

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            CheckClick(true);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            CheckClick(false);
    }

    void CheckClick(bool pressed)
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.transform == transform)
            {
                if (pressed)
                    KnopfRunter();
                else
                    KnopfHoch();
            }
        }
    }

    void KnopfRunter()
    {
        transform.localPosition = gedruecktPosition;

        if (!istGedrueckt)
        {
            istGedrueckt = true;
            SendTrue();
        }
    }

    void KnopfHoch()
    {
        transform.localPosition = startPosition;

        if (istGedrueckt)
        {
            istGedrueckt = false;
            SendFalse();
        }
    }

    void SendTrue()
    {
        Debug.Log($"Taster GEDRUECKT ({roomName})");
        SetInputLed(true);
        ToggleOutputState();

        if (client != null)
            client.WriteNode($"ns=6;s=::{roomName}:SwitchValueW", true);
    }

    void SendFalse()
    {
        Debug.Log($"Taster LOSGELASSEN ({roomName})");
        SetInputLed(false);

        if (client != null)
            client.WriteNode($"ns=6;s=::{roomName}:SwitchValueW", false);
    }

    void HandleDataChanged(object sender, OpcDataChangeReceivedEventArgs e)
    {
        OpcMonitoredItem item = (OpcMonitoredItem)sender;

        if (item.NodeId.ToString().Contains("ns=6;s=::AsGlobalPV:gSchweibsChange"))
        {
            string val = e.Item.Value.Value?.ToString() ?? "null";
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (statusText != null)
                    statusText.text = val;
            });
        }
        else if (item.NodeId.ToString().Contains($"::{roomName}:Lampe"))
        {
            bool lampState = false;
            if (e.Item.Value.Value != null)
                lampState = (bool)e.Item.Value.Value;

            Debug.Log($"Lampe ({roomName}) OPC: " + lampState);

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                lichtAn = lampState;
                if (zielLicht != null)
                    zielLicht.enabled = lampState;
                SetOutputLed(lampState);
                if (statusText4 != null)
                    statusText4.text = roomName + " Lampe: " + lampState.ToString();
            });
        }
        else
        {
            Debug.Log("Data Change: " + item.NodeId + " = " + e.Item.Value);
        }
    }

    void OnDestroy()
    {
        SetInputLed(false);
        SetOutputLed(false);

        try { subscription?.Unsubscribe(); } catch { }
        try { client?.Disconnect(); } catch { }
        try { client?.Dispose(); } catch { }
    }

    void SetInputLed(bool isOn)
    {
        if (updateX20Leds && x20LedController != null)
            x20LedController.SetInputLed(roomName, isOn);
    }

    void SetOutputLed(bool isOn)
    {
        if (updateX20Leds && x20LedController != null)
            x20LedController.SetOutputLed(roomName, isOn);
    }

    void ToggleOutputState()
    {
        lichtAn = !lichtAn;

        if (zielLicht != null)
            zielLicht.enabled = lichtAn;

        SetOutputLed(lichtAn);

        if (statusText4 != null)
            statusText4.text = roomName + " Lampe: " + lichtAn.ToString();
    }
}
