using UnityEngine;
using UnityEngine.InputSystem;

public class HoldLightController : MonoBehaviour
{
    public Light directionalLight;

    private Vector3 offRotation = new Vector3(8f, 42.483f, 0f);
    private Vector3 onRotation = new Vector3(-8f, 42.483f, 0f);

    void Start()
    {
        directionalLight.enabled = false;
        transform.rotation = Quaternion.Euler(offRotation);

        Debug.Log("Start: Licht AUS");
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckClick(true);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            CheckClick(false);
        }
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
                {
                    TurnLightOn();
                }
                else
                {
                    TurnLightOff();
                }
            }
        }
    }

    void TurnLightOn()
    {
        directionalLight.enabled = true;
        transform.rotation = Quaternion.Euler(onRotation);

        Debug.Log("Licht AN");
    }

    void TurnLightOff()
    {
        directionalLight.enabled = false;
        transform.rotation = Quaternion.Euler(offRotation);

        Debug.Log("Licht AUS");
    }
}