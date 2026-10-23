using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Taps on the tank: in feed mode (the Feed button toggles it) a tap on the water drops a pellet
// right where it touched, and a tap through the glass drops one on the surface above a random spot
// along the line the tap makes straight into the tank; otherwise a well-fed fish facing the viewer
// flares at the tap, or it swims over to where you tapped, and a tap right on the fish startles it into darting away.
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
    public float fishTapRadiusInches = 0.25f;     // how close to a bone (body or fin) a tap must land, on screen
    public float pelletWallInset = 0.04f;         // how close to the glass a pellet may float

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
        if (FeedMode)
        {
            if (tapToFeed && TapToWater(screenPosition, out Vector3 drop)) FeedAt(drop);
            return;
        }
        // Well fed and facing the viewer, any tap is a rival to display at.
        Camera cam = viewCamera ? viewCamera : Camera.main;
        if (fish != null && cam != null && fish.WantsToDisplay(cam.transform.position)) { fish.Display(cam.transform.position); return; }
        if (fish != null && TapHitsFish(screenPosition, out Vector3 hit)) { fish.Startle(hit); return; }
        if (tapToSwim && fish != null && TapToTank(screenPosition, out Vector3 point)) fish.SwimTo(point);
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

    // Where a feed tap goes: the tap ray is followed through the water (the swim bounds up to the surface).
    // If it crosses the surface inside the tank, going in from above or out from below (the camera
    // usually sits under the waterline), it is that very spot; otherwise it went in and out through
    // the glass and it is a random point on that stretch, which Drop then lifts to the surface.
    bool TapToWater(Vector2 screenPosition, out Vector3 point)
    {
        point = default;
        Camera cam = viewCamera ? viewCamera : Camera.main;
        if (cam == null || fish == null || fish.swimBounds == null) return false;
        Bounds b = fish.swimBounds.bounds;
        float surface = spawnPoint != null ? spawnPoint.position.y : b.max.y;
        Bounds water = new Bounds();
        water.SetMinMax(b.min, new Vector3(b.max.x, Mathf.Max(surface, b.min.y), b.max.z));
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (!RayThroughBox(ray, water, out float enter, out float exit)) return false;
        Vector3 inPoint = ray.GetPoint(enter), outPoint = ray.GetPoint(exit);
        float top = water.max.y - 1e-3f;
        point = inPoint.y >= top ? inPoint : outPoint.y >= top ? outPoint : ray.GetPoint(Random.Range(enter, exit));
        return true;
    }

    // Slab test: the distances along the ray where it enters and leaves the box.
    static bool RayThroughBox(Ray ray, Bounds box, out float enter, out float exit)
    {
        enter = 0f;
        exit = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            float o = ray.origin[i], d = ray.direction[i];
            if (Mathf.Abs(d) < 1e-6f)
            {
                if (o < box.min[i] || o > box.max[i]) return false;
                continue;
            }
            float t0 = (box.min[i] - o) / d, t1 = (box.max[i] - o) / d;
            if (t0 > t1) (t0, t1) = (t1, t0);
            enter = Mathf.Max(enter, t0);
            exit = Mathf.Min(exit, t1);
            if (enter > exit) return false;
        }
        return true;
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
        float inset = pelletWallInset;
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
