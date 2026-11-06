using UnityEngine;
using UnityEngine.InputSystem;

public class Schalter : MonoBehaviour
{
    public Light zielLicht;

    private Vector3 startPosition = new Vector3(0f, 0.00901f, 0f);
    private Vector3 gedruecktPosition = new Vector3(0f, 0.00824f, 0f);

    private bool istGedrueckt = false;
    private bool lichtAn = false;

    void Start()
    {
        zielLicht.enabled = false;
        transform.localPosition = startPosition;

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
                    KnopfRunter();
                }
                else
                {
                    KnopfHoch();
                }
            }
        }
    }

    void KnopfRunter()
    {
        transform.localPosition = gedruecktPosition;

        if (!istGedrueckt)
        {
            lichtAn = !lichtAn;
            zielLicht.enabled = lichtAn;

            Debug.Log("Licht: " + (lichtAn ? "AN" : "AUS"));
        }

        istGedrueckt = true;
    }

    void KnopfHoch()
    {
        transform.localPosition = startPosition;
        istGedrueckt = false;
    }
}