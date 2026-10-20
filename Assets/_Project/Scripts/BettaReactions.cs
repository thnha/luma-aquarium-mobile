using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

// Drives the Animator clips the AI doesn't trigger itself:
// - Resting follows BettaFishAI.State.Rest (folded fins loop),
// - tapping the fish plays Flare (GDD: touch reaction).
// A tap is a short press that barely moves, so orbit drags on OrbitTouchCamera don't flare.
public class BettaReactions : MonoBehaviour
{
    public BettaFishAI fish;
    public Animator animator;
    public Camera viewCamera;
    public float flareCooldown = 2f;
    public float maxTapSeconds = 0.3f;
    public float maxTapMovePixels = 20f;

    static readonly int RestingId = Animator.StringToHash("Resting");
    static readonly int FlareId = Animator.StringToHash("Flare");

    Renderer[] renderers;
    float nextFlareTime;
    Vector2 pressPosition;
    float pressTime;

    void Awake()
    {
        if (fish == null) fish = GetComponent<BettaFishAI>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable() { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void Update()
    {
        if (animator == null) return;
        animator.SetBool(RestingId, fish != null && fish.IsResting);

        if (TryGetTap(out Vector2 screenPosition) && Time.time >= nextFlareTime && IsOnFish(screenPosition))
            Flare();
    }

    public void Flare()
    {
        if (animator == null) return;
        animator.SetTrigger(FlareId);
        nextFlareTime = Time.time + flareCooldown;
    }

    bool TryGetTap(out Vector2 screenPosition)
    {
        screenPosition = default;
        var touches = Touch.activeTouches;
        if (touches.Count == 1)
        {
            Touch touch = touches[0];
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Ended) return false;
            screenPosition = touch.screenPosition;
            return touch.time - touch.startTime <= maxTapSeconds
                && (touch.screenPosition - touch.startScreenPosition).magnitude <= maxTapMovePixels
                && !IsOverUI();
        }
        if (touches.Count > 0 || Mouse.current == null) return false;

        var mouse = Mouse.current;
        if (mouse.leftButton.wasPressedThisFrame) { pressPosition = mouse.position.ReadValue(); pressTime = Time.unscaledTime; }
        if (!mouse.leftButton.wasReleasedThisFrame) return false;
        screenPosition = mouse.position.ReadValue();
        return Time.unscaledTime - pressTime <= maxTapSeconds
            && (screenPosition - pressPosition).magnitude <= maxTapMovePixels
            && !IsOverUI();
    }

    // Bounds test instead of colliders: SwimBounds is a trigger box around the fish
    // and would swallow physics raycasts.
    bool IsOnFish(Vector2 screenPosition)
    {
        Camera cam = viewCamera ? viewCamera : Camera.main;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screenPosition);
        foreach (var r in renderers)
            if (r.enabled && r.bounds.IntersectRay(ray)) return true;
        return false;
    }

    // InputSystemUIInputModule tracks the last pointer itself, for both touch and mouse.
    static bool IsOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
