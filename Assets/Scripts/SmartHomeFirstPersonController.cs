using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public sealed class SmartHomeFirstPersonController : MonoBehaviour
{
    [SerializeField] private Transform viewCamera;
    [SerializeField] private string roomRootName = "ModellSmartHomeRaum";
    [SerializeField] private bool keepInsideRoomBounds = true;
    [SerializeField] private float moveSpeed = 2.4f;
    [SerializeField] private float sprintSpeed = 4f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float keyboardTurnSpeed = 100f;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float roomEdgePadding = 0.35f;

    private CharacterController characterController;
    private Bounds roomBounds;
    private bool hasRoomBounds;
    private float pitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (viewCamera == null)
        {
            var camera = GetComponentInChildren<Camera>();
            viewCamera = camera != null ? camera.transform : null;
        }

        ConfigureCharacterController();
        ConfigureCamera();
    }

    private void Start()
    {
        CacheRoomBounds();
        ClampToRoomBounds();
        UnlockCursor();
    }

    private void OnDisable()
    {
        UnlockCursor();
    }

    private void Update()
    {
        HandleCursor();
        HandleLook();
        HandleMove();
    }

    private void ConfigureCharacterController()
    {
        characterController.height = 1.8f;
        characterController.radius = 0.3f;
        characterController.center = new Vector3(0f, characterController.height * 0.5f, 0f);
        characterController.stepOffset = 0.25f;
        characterController.slopeLimit = 45f;
    }

    private void ConfigureCamera()
    {
        if (viewCamera == null)
        {
            return;
        }

        viewCamera.localPosition = new Vector3(0f, eyeHeight, 0f);
        viewCamera.localRotation = Quaternion.identity;
    }

    private void HandleCursor()
    {
        if (WasEscapePressed())
        {
            UnlockCursor();
        }

        if (WasPrimaryPressed())
        {
            LockCursor();
        }
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked || viewCamera == null)
        {
            return;
        }

        var mouseDelta = GetLookDelta() * mouseSensitivity;
        transform.Rotate(Vector3.up, mouseDelta.x, Space.World);
        transform.Rotate(Vector3.up, GetKeyboardTurnInput() * keyboardTurnSpeed * Time.deltaTime, Space.World);

        pitch = Mathf.Clamp(pitch - mouseDelta.y, -78f, 78f);
        viewCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMove()
    {
        var input = GetMoveInput();
        var direction = transform.right * input.x + transform.forward * input.y;
        direction.y = 0f;

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        var speed = IsSprintHeld() ? sprintSpeed : moveSpeed;
        var delta = direction * (speed * Time.deltaTime);
        characterController.Move(delta);
        ClampToRoomBounds();
    }

    private void CacheRoomBounds()
    {
        if (!keepInsideRoomBounds || string.IsNullOrWhiteSpace(roomRootName))
        {
            return;
        }

        var roomRoot = GameObject.Find(roomRootName);
        if (roomRoot == null)
        {
            return;
        }

        var renderers = roomRoot.GetComponentsInChildren<Renderer>();
        foreach (var roomRenderer in renderers)
        {
            if (!hasRoomBounds)
            {
                roomBounds = roomRenderer.bounds;
                hasRoomBounds = true;
            }
            else
            {
                roomBounds.Encapsulate(roomRenderer.bounds);
            }
        }
    }

    private void ClampToRoomBounds()
    {
        if (!hasRoomBounds)
        {
            return;
        }

        var position = transform.position;
        var minX = roomBounds.min.x + roomEdgePadding;
        var maxX = roomBounds.max.x - roomEdgePadding;
        var minZ = roomBounds.min.z + roomEdgePadding;
        var maxZ = roomBounds.max.z - roomEdgePadding;

        if (minX < maxX)
        {
            position.x = Mathf.Clamp(position.x, minX, maxX);
        }

        if (minZ < maxZ)
        {
            position.z = Mathf.Clamp(position.z, minZ, maxZ);
        }

        transform.position = position;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static Vector2 GetMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        var input = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        return input;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    private static Vector2 GetLookDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

    private static bool IsSprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
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
        if (keyboard.eKey.isPressed) value += 1f;
        return value;
#elif ENABLE_LEGACY_INPUT_MANAGER
        var value = 0f;
        if (Input.GetKey(KeyCode.Q)) value -= 1f;
        if (Input.GetKey(KeyCode.E)) value += 1f;
        return value;
#else
        return 0f;
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

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }
}
