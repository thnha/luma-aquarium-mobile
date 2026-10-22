using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

// Restricted orbit around the tank using the new Input System.
// Yaw/pitch/distance limits keep the camera in front of the glass.
public class OrbitTouchCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 4.5f;
    public float minDistance = 2.5f;
    public float maxDistance = 6.5f;
    public float rotateSensitivity = 0.14f;
    public float zoomSensitivity = 0.007f;
    public float mouseRotateSensitivity = 0.2f;
    public float yaw = 0f;
    public float pitch = 8f;
    public float maxYaw = 50f;
    public float minPitch = -20f;
    public float maxPitch = 35f;

    [Tooltip("Half the tank width to keep in view on narrow portrait screens. 0 = use distance as-is.")]
    public float fitHalfWidth = 1.55f;
    public float fitDepthOffset = 0.55f;
    [Tooltip("How far the view slides sideways from the target in a tank wider than the screen (AquariumTank). 0 = no slide.")]
    public float panLimit;
    public float pan;

    void Start() { FitToScreen(); }

    // Portrait phones have a narrow horizontal FOV, so derive distances from the aspect ratio.
    public void FitToScreen()
    {
        var cam = GetComponent<Camera>();
        if (cam == null || fitHalfWidth <= 0f) return;
        float halfHFov = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect);
        float fit = fitHalfWidth / Mathf.Tan(halfHFov) + fitDepthOffset;
        distance = fit;
        maxDistance = fit * 1.15f;
        minDistance = Mathf.Min(minDistance, fit * 0.5f);
    }

    void OnEnable() { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void LateUpdate()
    {
        if (target == null) return;

        var touches = Touch.activeTouches;
        if (touches.Count == 1)
        {
            Touch touch = touches[0];
            if (!IsOverUI() && touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                Drag(touch.delta, rotateSensitivity);
        }
        else if (touches.Count == 2)
        {
            Touch a = touches[0], b = touches[1];
            float previous = ((a.screenPosition - a.delta) - (b.screenPosition - b.delta)).magnitude;
            float current = (a.screenPosition - b.screenPosition).magnitude;
            Zoom((current - previous) * zoomSensitivity);
        }
        else if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed && !IsOverUI())
                Drag(Mouse.current.delta.ReadValue(), mouseRotateSensitivity);
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) Zoom(Mathf.Sign(scroll) * 0.25f);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        transform.position = target.position + Vector3.right * pan - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }

    // In a tank wider than the screen a sideways swipe slides along the glass, the sand following the
    // finger; otherwise it turns the view a little. Up and down always tilt.
    void Drag(Vector2 pixels, float sensitivity)
    {
        if (panLimit <= 0f) { Rotate(pixels * sensitivity); return; }
        float worldPerPixel = fitHalfWidth * 2f / Mathf.Max(1, Screen.width);
        pan = Mathf.Clamp(pan - pixels.x * worldPerPixel, -panLimit, panLimit);
        Rotate(new Vector2(0, pixels.y) * sensitivity);
    }

    void Rotate(Vector2 delta)
    {
        yaw = Mathf.Clamp(yaw + delta.x, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
    }

    void Zoom(float amount)
    {
        distance = Mathf.Clamp(distance - amount, minDistance, maxDistance);
    }

    // InputSystemUIInputModule tracks the last pointer itself, for both touch and mouse.
    static bool IsOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
