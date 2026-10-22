using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Taps on the tank: in feed mode (the Feed button toggles it) a tap drops a pellet on the
// surface above the tapped spot; otherwise the fish swims over to where you tapped,
// and a tap right on the fish startles it into darting away.
// A tap is a short press that barely moves, so orbit drags on OrbitTouchCamera don't count.
public class FeedingSystem : MonoBehaviour
{
    public BettaFishAI fish;
    public FoodPellet pelletPrefab;
    public Transform spawnPoint;
    public float feedCooldown = 1f;
    public int maxActivePellets = 3;
    public float spawnJitter = 0.15f;

    [Header("Tap to feed")]
    public bool tapToFeed = true;
    public bool tapToSwim = true;
    public Camera viewCamera;
    public float tapFeedCooldown = 0.35f;
    public int maxTapPellets = 6;
    public float maxTapSeconds = 0.35f;
    public float maxTapMovePixels = 20f;
    public float tapDepthJitter = 0.12f;          // spread of dropped pellets front to back
    public float fishTapRadiusInches = 0.25f;     // how close to a bone (body or fin) a tap must land, on screen

    private readonly List<FoodPellet> pellets = new List<FoodPellet>();
    private float nextFeedTime;
    private Vector2 pressPosition;
    private float pressTime;
    private AquariumDecoration decoration;

    public bool FeedMode { get; private set; }
    public event System.Action<bool> FeedModeChanged;

    public void SetFeedMode(bool on)
    {
        if (FeedMode == on) return;
        FeedMode = on;
        FeedModeChanged?.Invoke(on);
    }

    void Start()
    {
        decoration = FindFirstObjectByType<AquariumDecoration>();
    }

    void Update()
    {
        if (!TryGetTap(out Vector2 screenPosition)) return;
        if (decoration != null && decoration.Editing) return;
        if (!FeedMode && fish != null && TapHitsFish(screenPosition, out Vector3 hit)) { fish.Startle(hit); return; }
        if (!TapToTank(screenPosition, out Vector3 point)) return;
        if (FeedMode)
        {
            // The tap only fixes left/right; spread the drops front to back.
            point.z += Random.Range(-tapDepthJitter, tapDepthJitter);
            if (tapToFeed) FeedAt(point);
        }
        else if (tapToSwim && fish != null) fish.SwimTo(point);
    }

    public void Feed()
    {
        if (spawnPoint == null) return;
        Vector2 jitter = Random.insideUnitCircle * spawnJitter;
        Drop(spawnPoint.position + new Vector3(jitter.x, 0f, jitter.y), feedCooldown, maxActivePellets);
    }

    // Drops a pellet on the surface above a point in the tank.
    public void FeedAt(Vector3 point) => Drop(point, tapFeedCooldown, maxTapPellets);

    void Drop(Vector3 position, float cooldown, int maxPellets)
    {
        pellets.RemoveAll(p => p == null);
        if (Time.time < nextFeedTime || pelletPrefab == null || pellets.Count >= maxPellets) return;
        nextFeedTime = Time.time + cooldown;

        if (spawnPoint != null) position.y = spawnPoint.position.y;
        position = ClampToTank(position);
        FoodPellet pellet = Instantiate(pelletPrefab, position, Quaternion.identity);
        if (fish != null) pellet.groundHeight = fish.GroundHeight;
        pellets.Add(pellet);
        if (fish != null) fish.Seek(NearestPellet(fish.transform.position));
    }

    // Where the tap ray crosses the tank's middle plane (front to back).
    bool TapToTank(Vector2 screenPosition, out Vector3 point)
    {
        point = default;
        Camera cam = viewCamera ? viewCamera : Camera.main;
        if (cam == null || fish == null || fish.swimBounds == null) return false;
        Bounds b = fish.swimBounds.bounds;
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (!new Plane(Vector3.forward, b.center).Raycast(ray, out float distance)) return false;
        point = ray.GetPoint(distance);
        return point.x >= b.min.x && point.x <= b.max.x;
    }

    // Tests the tap against the fish's skeleton in screen space: every bone (spine, fin rays out
    // to the tips) and the segment to its parent, so tapping a fin counts as much as the body.
    // fishTapRadiusInches is a fingertip's reach on screen, plus a little for the flesh around the bones.
    bool TapHitsFish(Vector2 screenPosition, out Vector3 hit)
    {
        hit = default;
        Camera cam = viewCamera ? viewCamera : Camera.main;
        if (cam == null) return false;
        float dpi = Screen.dpi > 0f ? Screen.dpi : 96f;
        float radius = fishTapRadiusInches * dpi;
        float best = radius * radius;
        bool found = false;
        foreach (var skin in fish.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled) continue;
            foreach (Transform bone in skin.bones)
            {
                if (bone == null) continue;
                Vector3 a = cam.WorldToScreenPoint(bone.position);
                if (a.z <= 0f) continue;
                Vector3 b = bone.parent != null ? cam.WorldToScreenPoint(bone.parent.position) : a;
                // Closest point on the bone segment to the finger, on screen.
                Vector2 ab = (Vector2)b - (Vector2)a;
                float k = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(screenPosition - (Vector2)a, ab) / ab.sqrMagnitude) : 0f;
                float d = (screenPosition - ((Vector2)a + ab * k)).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                hit = Vector3.Lerp(bone.position, bone.parent != null ? bone.parent.position : bone.position, k);
                found = true;
            }
        }
        return found;
    }

    Vector3 ClampToTank(Vector3 position)
    {
        if (fish == null || fish.swimBounds == null) return position;
        Bounds b = fish.swimBounds.bounds;
        float inset = fish.boundsInset;
        position.x = Mathf.Clamp(position.x, b.min.x + inset, b.max.x - inset);
        position.z = Mathf.Clamp(position.z, b.min.z + inset, b.max.z - inset);
        return position;
    }

    bool TryGetTap(out Vector2 screenPosition)
    {
        screenPosition = default;
        var pointer = Pointer.current;
        if (pointer == null) return false;
        if (pointer.press.wasPressedThisFrame)
        {
            pressPosition = pointer.position.ReadValue();
            pressTime = Time.unscaledTime;
        }
        if (!pointer.press.wasReleasedThisFrame) return false;
        screenPosition = pressPosition;
        return Time.unscaledTime - pressTime <= maxTapSeconds
            && (pointer.position.ReadValue() - pressPosition).magnitude <= maxTapMovePixels
            && !IsOverUI();
    }

    // InputSystemUIInputModule tracks the last pointer itself, for both touch and mouse.
    static bool IsOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public FoodPellet NearestPellet(Vector3 from)
    {
        FoodPellet best = null;
        float bestDistance = float.MaxValue;
        for (int i = pellets.Count - 1; i >= 0; i--)
        {
            FoodPellet p = pellets[i];
            if (p == null) { pellets.RemoveAt(i); continue; }
            if (p.Unreachable) continue;
            float d = (p.transform.position - from).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = p; }
        }
        return best;
    }
}
