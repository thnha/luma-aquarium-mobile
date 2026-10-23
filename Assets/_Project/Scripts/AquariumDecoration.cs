using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Placement is a transaction: only Confirm writes the layout; Cancel restores the object.
public sealed class AquariumDecoration : MonoBehaviour
{
    public AquariumDecorItem[] prefabs;
    public AquariumDecorCatalog catalog;
    // Wall and floor styles; AquariumTank reads it (the tank component is added at runtime).
    public AquariumSurfaceCatalog surfaces;
    public string inventoryStorageKey = Lumaquarium.UI.FishShopInventory.DefaultStorageKey;
    public Transform itemsRoot;
    public Camera viewCamera;
    public OrbitTouchCamera orbit;
    public Lumaquarium.UI.MainAquariumScreen screen;
    public GameObject dock;
    public GameObject tiltButton, trimButton, growButton;
    // Shown while nothing is selected: Thêm opens the storage sheet, Hoàn tác undoes the last Đặt or Cất, Xong leaves.
    public GameObject addButton, undoButton, doneButton;
    public TMP_Text instruction;
    public LineRenderer selectionRing;
    public AquariumWaterEffects water;
    public Vector2 min = new Vector2(-.74f, -.5f);
    public Vector2 max = new Vector2(.74f, .55f);
    public float floorY = .31f;
    // The sand rises by sandRise toward the back, between world z sandRiseFrom and sandRiseTo.
    public float sandRise;
    public float sandRiseFrom = .3f, sandRiseTo = 2.2f;
    // Capacity in points: a small piece costs 1, larger pieces up to 3 (AquariumDecorItem.points).
    public int capacity = 24;
    public string storageKey = "Lumaquarium.Decoration.Preview.v1";
    public bool persistLayout = true;
    // Placement looks down on the sand at this angle, so the back of the tank gets as much screen as the front.
    public float placementPitch = 50;
    [Range(0, .8f)] public float footprintOverlap = .35f;

    public bool Editing { get; private set; }
    public AquariumDecorItem Selected { get; private set; }
    private bool isNew;
    private AquariumCarpet carpet;
    // Carpet brush: the species being painted (0 when not brushing), its shop kind, erase mode and the
    // carpet as it was when the brush opened, for Hủy.
    private int brushSpecies, brushKind;
    private bool erasing;
    private byte[] carpetBefore;
    private readonly Dictionary<GameObject, string> dockLabels = new Dictionary<GameObject, string>();
    public bool Brushing => brushSpecies > 0;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private Transform originalParent;
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private BettaReactions reactions;
    private bool reactionsEnabled;
    private bool pressInTank;
    // A press that starts on the selected piece and ends without travelling places it.
    private Vector2 pressStart;
    private bool pressOnSelected, dragged;
    // The press picked a piece: it never places it on release.
    private bool pickedThisPress;
    private float TapSlop => Screen.height * .012f;
    // The layout when the current piece was picked up, and the layout and carpet before the last
    // Đặt or Cất, which Hoàn tác brings back (one step).
    private Layout before;
    private Layout undoLayout;
    private byte[] undoCarpet;
    private Lumaquarium.UI.DecorInventoryPanel inventory;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly List<AquariumDecorItem> items = new List<AquariumDecorItem>();
    // World-space vertices of each placed stone or piece of wood, read when something is stacked on it.
    private readonly Dictionary<AquariumDecorItem, (Vector3[] points, Bounds bounds)> surfaceCache =
        new Dictionary<AquariumDecorItem, (Vector3[] points, Bounds bounds)>();
    public int Count => items.Count;
    public int Points
    {
        get
        {
            int points = 0;
            foreach (var item in items) if (item != null) points += Mathf.Max(1, item.points);
            return points;
        }
    }

    public bool HasRoomFor(int kind) =>
        kind >= 0 && kind < prefabs.Length && prefabs[kind] != null &&
        (prefabs[kind].carpet > 0 || Points + Mathf.Max(1, prefabs[kind].points) <= capacity);

    [Serializable] private class Layout { public List<Entry> items = new List<Entry>(); }
    [Serializable] private class Entry
    {
        public int kind;
        public float x, y, z, yaw, tilt, height;
        public string id, parentId;
    }

    public int Available(int kind)
    {
        var offer = catalog != null ? catalog.Find(kind) : null;
        if (offer == null) return catalog == null && kind < 3 ? capacity : 0;
        var inventory = new Lumaquarium.UI.FishShopInventory(inventoryStorageKey);
        return Mathf.Max(0, inventory.DecorCount(offer.id) - items.FindAll(x => x != null && x.kind == kind).Count);
    }

    // How many of each kind are placed; the shop reads this once per refresh instead of calling Available per card.
    public Dictionary<int, int> PlacedCounts()
    {
        var counts = new Dictionary<int, int>();
        foreach (var item in items)
            if (item != null) counts[item.kind] = counts.TryGetValue(item.kind, out int n) ? n + 1 : 1;
        return counts;
    }

    // Height of the sand at a point; the sand bed mesh is shaped with the same curve.
    public float SandAt(Vector3 p) => floorY + sandRise * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(sandRiseFrom, sandRiseTo, p.z));

    private float Height(AquariumDecorItem item, Vector3 p) => item != null && item.floats && water != null ? water.surfaceY - .012f : SandAt(p);
    private DecorRole RoleOf(AquariumDecorItem item) => DecorPlacementRules.Role(item, catalog);
    // Moss and rooted plants can be mounted on a host's plant mounts.
    private bool Mountable(AquariumDecorItem item) => item != null && (RoleOf(item) == DecorRole.Moss || RoleOf(item) == DecorRole.Plant);
    private bool Mounts(AquariumDecorItem item, AquariumDecorItem host) =>
        item != null && host != null && host != item && host.supportsPlants && host.plantMounts != null &&
        host.plantMounts.Length > 0 && items.Contains(host) && DecorPlacementRules.CanMountOn(RoleOf(item), RoleOf(host));
    // The back glass is the far edge of the placement area; moss walls cover it up to below the surface.
    private float WallZ => max.y;
    private float WallTop => water != null ? water.surfaceY - .08f : floorY + 1.8f;

    // Where an item comes to rest for a dragged point: on the sand or the stone and wood below it,
    // on the surface, or on the back glass.
    // Settle passes a ceiling so a piece never rests on what is stacked on top of it.
    private Vector3 Rest(AquariumDecorItem item, Vector3 p, float ceiling = float.PositiveInfinity) =>
        item.wallMounted ? new Vector3(p.x, p.y, WallZ) :
        new Vector3(p.x, item.Stacks ? GroundAt(item, p, ceiling) : Height(item, p), p.z);

    // The highest stone or wood surface under a point, or the sand. Only surfaces below the ceiling
    // count, so settling never lifts a piece onto what is stacked on top of it.
    // Only pieces this one may stand on count (stone carries stone, wood and coral).
    private float GroundAt(AquariumDecorItem item, Vector3 p, float ceiling)
    {
        float sand = SandAt(p), top = sand, radius = Mathf.Max(.05f, item.footprint * .35f);
        foreach (var other in items)
        {
            if (other == null || other == item || other.transform.IsChildOf(item.transform) ||
                other.transform.parent != itemsRoot || !other.Stacks ||
                !DecorPlacementRules.CanStandOn(RoleOf(item), RoleOf(other))) continue;
            var surface = Surface(other, out var bounds);
            if (p.x < bounds.min.x - radius || p.x > bounds.max.x + radius || p.z < bounds.min.z - radius ||
                p.z > bounds.max.z + radius || bounds.max.y <= top) continue;
            foreach (var v in surface)
                if (v.y > top && v.y < ceiling && (v.x - p.x) * (v.x - p.x) + (v.z - p.z) * (v.z - p.z) < radius * radius)
                    top = v.y;
        }
        // Sink a little into the surface so the piece looks seated rather than balanced on a point.
        return top > sand ? top - .015f : sand;
    }

    private Vector3[] Surface(AquariumDecorItem item, out Bounds bounds)
    {
        if (surfaceCache.TryGetValue(item, out var cached)) { bounds = cached.bounds; return cached.points; }
        var points = new List<Vector3>();
        foreach (var filter in item.GetComponentsInChildren<MeshFilter>())
        {
            // Plants attached to the piece are not part of its surface.
            if (filter.sharedMesh == null || !filter.sharedMesh.isReadable || filter.GetComponentInParent<AquariumDecorItem>() != item) continue;
            var matrix = filter.transform.localToWorldMatrix;
            foreach (var v in filter.sharedMesh.vertices) points.Add(matrix.MultiplyPoint3x4(v));
        }
        var result = points.ToArray();
        bounds = result.Length > 0 ? GeometryUtility.CalculateBounds(result, Matrix4x4.identity) : new Bounds(item.transform.position, Vector3.zero);
        surfaceCache[item] = (result, bounds);
        return result;
    }

    // A piece resting on stone or wood rather than on the sand.
    private bool Raised(AquariumDecorItem item, Vector3 p) => item.Stacks && p.y > SandAt(p) + .03f;

    // After a piece is moved or put away, whatever stood on it drops onto the next surface down.
    private void Settle()
    {
        surfaceCache.Clear();
        var raised = items.FindAll(x => x != null && x.transform.parent == itemsRoot && Raised(x, x.transform.position));
        raised.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        foreach (var item in raised)
        {
            var p = item.transform.position;
            float y = GroundAt(item, p, p.y + .02f);
            if (Mathf.Abs(y - p.y) < .005f) continue;
            var to = new Vector3(p.x, y, p.z);
            // Dropped onto the sand it may land in another piece's footprint (stone off wood), so it slides free.
            item.transform.position = Mathf.Abs(y - SandAt(to)) < .005f ? SlideFree(item, to, p.y + .02f) : to;
            surfaceCache.Remove(item);
        }
    }

    // Gift packs first: the tank, the shop and the HUD read what is owned and in use from their Start.
    private void Awake()
    {
        if (catalog != null) new Lumaquarium.UI.FishShopInventory(inventoryStorageKey).ApplyGifts(catalog.giftPacks);
    }

    private void Start()
    {
        // The tank sets the placement area and capacity, so it comes before the layout is checked.
        var tank = GetComponent<AquariumTank>();
        if (tank == null) tank = gameObject.AddComponent<AquariumTank>();
        tank.Init(this);
        foreach (var item in itemsRoot.GetComponentsInChildren<AquariumDecorItem>())
        {
            if (string.IsNullOrEmpty(item.instanceId)) item.instanceId = Guid.NewGuid().ToString("N");
            items.Add(item);
        }
        if (persistLayout && PlayerPrefs.HasKey(storageKey)) LoadLayout();
        if (catalog != null)
        {
            var placed = new Dictionary<string, int>();
            foreach (var item in items)
            {
                var offer = catalog.Find(item.kind);
                if (item.kind >= 3 || offer == null) continue;
                placed[offer.id] = placed.TryGetValue(offer.id, out int count) ? count + 1 : 1;
            }
            new Lumaquarium.UI.FishShopInventory(inventoryStorageKey).KeepPlacedInStock(placed);
        }
        carpet = GetComponent<AquariumCarpet>();
        if (carpet == null) carpet = gameObject.AddComponent<AquariumCarpet>();
        carpet.Init(this);
        if (GetComponent<AquariumDepthOfField>() == null) gameObject.AddComponent<AquariumDepthOfField>();
        tiltButton = DockButton(tiltButton, "PlacementTilt", "Nghiêng", Tilt, 1);
        trimButton = DockButton(trimButton, "PlacementTrim", "Tỉa", Trim, 2);
        growButton = DockButton(growButton, "PlacementGrow", "Mọc", Grow, 3);
        addButton = DockButton(addButton, "PlacementAdd", "Thêm", Add, 7);
        undoButton = DockButton(undoButton, "PlacementUndo", "Hoàn tác", UndoLast, 8);
        doneButton = DockButton(doneButton, "PlacementDone", "Xong", Finish, 9, "PlacementAction3");
        inventory = screen.decoratePanel != null ? screen.decoratePanel.GetComponent<Lumaquarium.UI.DecorInventoryPanel>() : null;
        dock.SetActive(false);
        selectionRing.gameObject.SetActive(false);
    }

    // Docks built before a button existed get one copied from a dock button (Xoay unless given),
    // placed the given number of slots after Xoay.
    private GameObject DockButton(GameObject button, string name, string label, UnityEngine.Events.UnityAction action, int after,
        string from = "PlacementAction0")
    {
        if (button == null)
        {
            var rotate = dock.transform.Find("PlacementAction0");
            var source = dock.transform.Find(from);
            if (rotate == null || source == null) return null;
            button = Instantiate(source.gameObject, rotate.parent);
            button.name = name;
            button.transform.SetSiblingIndex(rotate.GetSiblingIndex() + after);
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = label;
            var control = button.GetComponent<UnityEngine.UI.Button>();
            control.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            control.onClick.AddListener(action);
        }
        button.SetActive(false);
        return button;
    }

    public void BeginPlant() => OpenShop(0);
    public void BeginRock() => OpenShop(2);
    public void BeginWood() => OpenShop(1);

    private void OpenShop(int category)
    {
        var shop = FindFirstObjectByType<Lumaquarium.UI.ShopScreen>(FindObjectsInactive.Include);
        if (shop != null) { shop.OpenTab(Lumaquarium.UI.ShopEntry.TabOf((AquariumDecorCategory)category)); return; }
        var old = FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>();
        if (old == null) { Begin(category == 0 ? 0 : category == 2 ? 1 : 2); return; }
        old.Open();
        old.SelectCategory(category);
    }

    public bool Begin(int kind)
    {
        if (kind < 0 || kind >= prefabs.Length || prefabs[kind] == null) return false;
        Cancel();
        if (Available(kind) <= 0) return false;
        if (!Editing) OpenEditor();
        if (prefabs[kind].carpet > 0) { BeginBrush(kind); return true; }
        if (!HasRoomFor(kind))
        {
            Idle();
            instruction.text = $"Hồ đã dùng {Points}/{capacity} điểm, món này cần {Mathf.Max(1, prefabs[kind].points)}. Cất bớt đồ để đặt.";
            return false;
        }
        before = Capture();
        Selected = Instantiate(prefabs[kind], itemsRoot);
        Selected.name = prefabs[kind].name;
        Selected.instanceId = Guid.NewGuid().ToString("N");
        Selected.transform.position = Rest(Selected, new Vector3(0, floorY + .3f, Mathf.Lerp(min.y, max.y, .4f)));
        FrameSelection();
        isNew = true;
        RefreshSelection();
        return true;
    }

    // The dock becomes the brush's: Tẩy/Trải switches erasing, Hủy undoes the session, Xong keeps it.
    private void BeginBrush(int kind)
    {
        brushKind = kind;
        brushSpecies = prefabs[kind].carpet;
        erasing = false;
        carpetBefore = carpet.Snapshot();
        Relabel("PlacementAction1", "Tẩy");
        Relabel("PlacementAction3", "Xong");
        ShowDock();
        BrushHint();
    }

    private int BrushAllowance => Available(brushKind) * AquariumCarpet.PackCells;

    private void BrushHint()
    {
        int left = Mathf.Max(0, BrushAllowance - carpet.Count(brushSpecies));
        instruction.text = erasing ? "Kéo trên cát để xoá thảm • Chọn Trải để trải tiếp" :
            left > 0 ? $"Kéo trên cát để trải thảm • Còn {left} ô • Chọn Xong để lưu" : "Đã trải hết số ô đã mua • Mua thêm gói trong cửa hàng";
    }

    private void Relabel(string name, string label)
    {
        var button = dock.transform.Find(name);
        var text = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
        if (text == null) return;
        if (!dockLabels.ContainsKey(button.gameObject)) dockLabels[button.gameObject] = text.text;
        text.text = label;
    }

    private void EndBrush()
    {
        if (!Brushing) return;
        brushSpecies = 0;
        carpetBefore = null;
        foreach (var entry in dockLabels)
        {
            var text = entry.Key.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = entry.Value;
        }
        dockLabels.Clear();
    }

    public void EditExisting()
    {
        if (!Editing) OpenEditor();
        Cancel();
    }

    private void OpenEditor()
    {
        cameraPosition = viewCamera.transform.position;
        cameraRotation = viewCamera.transform.rotation;
        Editing = true;
        Remember(null, null);
        surfaceCache.Clear();
        screen.ClosePanels();
        screen.toolbar.SetActive(false);
        dock.SetActive(true);
        orbit.enabled = false;
        FramePlacementArea();
        reactions = screen.fish != null ? screen.fish.GetComponent<BettaReactions>() : null;
        if (reactions != null) { reactionsEnabled = reactions.enabled; reactions.enabled = false; }
    }

    public void Select(AquariumDecorItem item)
    {
        if (!Editing || item == null || !items.Contains(item)) return;
        RestoreSelection();
        before = Capture();
        Selected = item;
        originalParent = item.transform.parent;
        originalPosition = item.transform.position;
        originalRotation = item.transform.rotation;
        originalScale = item.transform.localScale;
        isNew = false;
        FrameSelection();
        RefreshSelection();
    }

    private void FrameSelection()
    {
        if (Selected == null) return;
        if (Selected.floats)
        {
            float surface = Height(Selected, Selected.transform.position);
            viewCamera.transform.position = new Vector3(0, surface + 2f, -2.5f);
            viewCamera.transform.LookAt(new Vector3(0, surface, 0));
        }
        else if (Selected.wallMounted)
        {
            viewCamera.transform.position = new Vector3(0, 2.1f, -3.4f);
            viewCamera.transform.LookAt(new Vector3(0, .9f, 0));
        }
        else FramePlacementArea();
    }

    // Looks down on the whole placement area from above the front glass, as close as the screen allows:
    // the front edge stays above the placement dock and the back edge below the header.
    private void FramePlacementArea()
    {
        var target = new Vector3((min.x + max.x) * .5f, floorY, Mathf.Lerp(min.y, max.y, .4f));
        var rotation = Quaternion.Euler(placementPitch, 0, 0);
        float depth = max.y - min.y;
        for (float distance = depth * .8f; distance < depth * 4; distance += .05f)
        {
            viewCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            Vector3 frontLeft = viewCamera.WorldToViewportPoint(new Vector3(min.x, floorY, min.y));
            Vector3 frontRight = viewCamera.WorldToViewportPoint(new Vector3(max.x, floorY, min.y));
            Vector3 back = viewCamera.WorldToViewportPoint(new Vector3(min.x, SandAt(new Vector3(0, 0, max.y)), max.y));
            if (frontLeft.x >= .03f && frontRight.x <= .97f && frontLeft.y >= .27f && back.y <= .88f) return;
        }
    }

    private AquariumDecorItem WoodAlongRay(Ray ray, out Vector3 point)
    {
        point = Vector3.zero;
        AquariumDecorItem wood = null;
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(ray, 30, 1 << 8, QueryTriggerInteraction.Collide))
        {
            var candidate = hit.collider.GetComponentInParent<AquariumDecorItem>();
            if (candidate == null || !Mounts(Selected, candidate) || hit.distance >= nearest) continue;
            wood = candidate; point = hit.point; nearest = hit.distance;
        }
        return wood;
    }

    public bool MoveAlongRay(Ray ray)
    {
        if (!Editing || Selected == null) return false;
        if (Selected.wallMounted)
            return new Plane(Vector3.back, new Vector3(0, 0, WallZ)).Raycast(ray, out float wallDistance)
                && MoveTo(ray.GetPoint(wallDistance) - Vector3.up * Selected.wallSize.y * .5f);
        if (Mountable(Selected))
        {
            var wood = WoodAlongRay(ray, out var hitPoint);
            if (wood != null) return AttachTo(wood, hitPoint);
        }
        return SurfaceHit(ray, Selected, out var point) && MoveTo(point);
    }

    // Where the ray meets the sand (or the water surface for floating plants). The sloped sand is
    // found by meeting the flat height first, then refining at the sand height found there.
    private bool SurfaceHit(Ray ray, AquariumDecorItem item, out Vector3 point)
    {
        point = default;
        float height = Height(item, new Vector3(0, 0, min.y));
        for (int step = 0; step < 3; step++)
        {
            if (!new Plane(Vector3.up, new Vector3(0, height, 0)).Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance);
            height = Height(item, point);
        }
        return true;
    }

    public bool MoveTo(Vector3 position)
    {
        if (!Editing || Selected == null || !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return false;
        Selected.transform.SetParent(itemsRoot, true);
        // Dragged past the glass the piece stops against it, and dragged onto another piece it slides
        // off to the nearest free spot, so it only turns red where there is truly no room.
        Selected.transform.position = Selected.wallMounted ? Rest(Selected, Inside(Selected, position)) : SlideFree(Selected, position);
        RefreshSelection();
        return CanPlace(Selected, Selected.transform.position);
    }

    public bool AttachTo(AquariumDecorItem wood, Vector3 worldPosition)
    {
        if (!Editing || Selected == null || !Mounts(Selected, wood)) return false;
        Vector3 nearest = wood.plantMounts[0];
        float distance = float.PositiveInfinity;
        foreach (var mount in wood.plantMounts)
        {
            float next = (wood.transform.TransformPoint(mount) - worldPosition).sqrMagnitude;
            if (next < distance) { distance = next; nearest = mount; }
        }
        Selected.transform.SetParent(wood.transform, true);
        Selected.transform.localPosition = nearest;
        RefreshSelection();
        return CanPlace(Selected, Selected.transform.position);
    }

    public bool CanPlace(AquariumDecorItem item, Vector3 p)
    {
        if (item == null || !float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) return false;
        var host = item.transform.parent != null ? item.transform.parent.GetComponent<AquariumDecorItem>() : null;
        if (host != null)
        {
            if (!Mounts(item, host)) return false;
            foreach (var other in items)
                if (other != null && other != item && other.transform.parent == host.transform &&
                    Vector3.Distance(p, other.transform.position) < .045f) return false;
            return true;
        }
        if (item.wallMounted)
        {
            Vector2 half = item.wallSize * .5f;
            if (p.x - half.x < min.x || p.x + half.x > max.x || p.y < SandAt(p) - .001f || p.y + item.wallSize.y > WallTop) return false;
            // Neighbouring panels may overlap a little so the moss closes the seam between them.
            foreach (var other in items)
                if (other != null && other != item && other.wallMounted && other.transform.parent == itemsRoot &&
                    Mathf.Abs(p.x - other.transform.position.x) < half.x + other.wallSize.x * .5f - .03f &&
                    Mathf.Abs(p.y + half.y - other.transform.position.y - other.wallSize.y * .5f) < half.y + other.wallSize.y * .5f - .03f)
                    return false;
            return true;
        }
        Vector2 reach = Reach(item);
        const float slack = .001f;
        if (p.x - reach.x < min.x - slack || p.x + reach.x > max.x + slack || p.z - reach.y < min.y - slack || p.z + reach.y > max.y + slack) return false;
        // Stacked pieces sit on top of others, so their footprints may overlap.
        if (Raised(item, p)) return true;
        foreach (var other in items)
        {
            if (!Crowds(item, other)) continue;
            Vector2 delta = new Vector2(p.x - other.transform.position.x, p.z - other.transform.position.z);
            float gap = Gap(item, other);
            if (delta.sqrMagnitude < gap * gap - slack) return false;
        }
        return true;
    }

    // Another piece on the sand (or the surface) whose footprint this one may not overlap too far.
    private bool Crowds(AquariumDecorItem item, AquariumDecorItem other) =>
        other != null && other != item && !other.transform.IsChildOf(item.transform) && other.transform.parent == itemsRoot &&
        other.floats == item.floats && !other.wallMounted && !Raised(other, other.transform.position);

    // Footprints are the base of a piece; neighbours may overlap them by this share, as plants
    // tucked against a stone do; rooted plants may stand almost inside the foot of a piece of wood.
    private float Gap(AquariumDecorItem item, AquariumDecorItem other) =>
        (item.footprint + other.footprint) * (1 - DecorPlacementRules.Overlap(RoleOf(item), RoleOf(other), footprintOverlap));

    // How far a piece reaches toward the side glass (x) and the front and back glass (y) from its pivot:
    // its footprint, or less along an axis where the piece is narrower, such as a branch laid along the glass.
    private Vector2 Reach(AquariumDecorItem item)
    {
        float r = item.footprint;
        Vector3 pivot = item.transform.position;
        float x = 0, z = 0;
        bool any = false;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>())
        {
            // Plants attached to the piece hang over it and do not hold it off the glass.
            if (renderer.GetComponentInParent<AquariumDecorItem>() != item) continue;
            var b = renderer.bounds;
            x = Mathf.Max(x, pivot.x - b.min.x, b.max.x - pivot.x);
            z = Mathf.Max(z, pivot.z - b.min.z, b.max.z - pivot.z);
            any = true;
        }
        return any ? new Vector2(Mathf.Min(r, x), Mathf.Min(r, z)) : new Vector2(r, r);
    }

    // Where a piece dragged to p comes to rest: inside the glass and, unless it stacks onto stone or
    // wood, pushed off any piece it overlaps too far. Falls back to the dragged spot when it is boxed in.
    private Vector3 SlideFree(AquariumDecorItem item, Vector3 p, float ceiling = float.PositiveInfinity)
    {
        Vector3 start = Rest(item, Inside(item, p), ceiling);
        p = start;
        for (int pass = 0; pass < 6; pass++)
        {
            if (Raised(item, p)) return p;
            Vector2 push = Vector2.zero;
            foreach (var other in items)
            {
                if (!Crowds(item, other)) continue;
                Vector2 delta = new Vector2(p.x - other.transform.position.x, p.z - other.transform.position.z);
                float gap = Gap(item, other), distance = delta.magnitude;
                if (distance >= gap) continue;
                Vector2 away = distance > 1e-4f ? delta / distance : Vector2.right;
                push += away * (gap - distance + .002f);
            }
            if (push == Vector2.zero) return p;
            p = Rest(item, Inside(item, p + new Vector3(push.x, 0, push.y)), ceiling);
        }
        return CanPlace(item, p) ? p : start;
    }

    public void Rotate()
    {
        if (Selected == null || Selected.wallMounted) return;
        Selected.transform.Rotate(0, 30, 0, Space.World);
        RefreshSelection();
    }

    // Leans driftwood sideways in 10° steps up to 30°, then stands it upright again.
    public void Tilt()
    {
        if (Selected == null || !Selected.tiltable || Selected.transform.parent != itemsRoot) return;
        var angles = Selected.transform.eulerAngles;
        float tilt = Mathf.DeltaAngle(0, angles.z);
        tilt = tilt >= 25 ? 0 : Mathf.Round(tilt / 10) * 10 + 10;
        Selected.transform.rotation = Quaternion.Euler(0, angles.y, tilt);
        RefreshSelection();
    }

    // Rooted plants can be trimmed to 0.6× or grown to 1.6× their height, in steps of 0.2.
    private static bool Trimmable(AquariumDecorItem item) => item != null && item.foliage != null && !item.floats && !item.wallMounted;
    public void Trim() => Resize(-.2f);
    public void Grow() => Resize(.2f);

    private void Resize(float step)
    {
        if (!Trimmable(Selected)) return;
        float height = Mathf.Clamp(Mathf.Round((Selected.transform.localScale.y + step) * 10) / 10, .6f, 1.6f);
        SetHeight(Selected, height);
        RefreshSelection();
        instruction.text = $"Chiều cao ×{height:0.0} • Tỉa hoặc Mọc tiếp, rồi chọn Đặt";
    }

    // Taller plants also spread a little, so the leaves don't look stretched.
    private static void SetHeight(AquariumDecorItem item, float height)
    {
        float spread = Mathf.Pow(height, .3f);
        item.transform.localScale = new Vector3(spread, height, spread);
    }

    // Đặt: places the selected piece, saves, and stays in the editor for the next piece.
    public bool ConfirmPlacement()
    {
        if (Brushing)
        {
            Remember(Capture(), carpetBefore);
            carpet.Save();
            EndBrush();
            Idle();
            return true;
        }
        if (Selected == null || !CanPlace(Selected, Selected.transform.position)) return false;
        if (isNew && (Points + Mathf.Max(1, Selected.points) > capacity || Available(Selected.kind) <= 0)) return false;
        if (isNew) items.Add(Selected);
        Remember(before, carpet.Snapshot());
        water?.Ripple(Selected.transform.position);
        Selected = null;
        isNew = false;
        Settle();
        SaveLayout();
        Idle();
        return true;
    }

    public void Confirm() => ConfirmPlacement();

    // Cất: the piece goes back to storage. A new piece that was never placed leaves nothing to undo.
    public void Delete()
    {
        if (Brushing)
        {
            erasing = !erasing;
            Relabel("PlacementAction1", erasing ? "Trải" : "Tẩy");
            BrushHint();
            return;
        }
        if (Selected == null) return;
        if (!isNew)
        {
            Remember(before, carpet.Snapshot());
            items.RemoveAll(x => x == Selected || (x != null && x.transform.IsChildOf(Selected.transform)));
        }
        Destroy(Selected.gameObject);
        Selected = null;
        isNew = false;
        Settle();
        SaveLayout();
        Idle();
    }

    // Hủy: puts the selected piece back where it was (a new one goes back to storage) and stays in the editor.
    public void Cancel()
    {
        if (Brushing) carpet.Restore(carpetBefore);
        EndBrush();
        RestoreSelection();
        if (Editing) Idle();
    }

    // Hoàn tác: the tank as it was before the last Đặt or Cất.
    public void UndoLast()
    {
        if (!Editing || Selected != null || Brushing || undoLayout == null) return;
        if (Apply(undoLayout))
        {
            if (undoCarpet != null) { carpet.Restore(undoCarpet); carpet.Save(); }
            SaveLayout();
        }
        Remember(null, null);
        Idle();
    }

    // Xong: places the selected piece if it can stand where it is (otherwise puts it back) and returns to the tank.
    public void Finish()
    {
        if (Brushing) ConfirmPlacement();
        else if (Selected != null && !ConfirmPlacement()) RestoreSelection();
        CloseEditor();
    }

    // Leaving for another screen: the piece being moved goes back, everything placed stays.
    public void Leave()
    {
        Cancel();
        CloseEditor();
    }

    // Tapping another piece places the selected one where it stands (or puts it back when it cannot stand
    // there) and picks the new one.
    private void Switch(AquariumDecorItem item)
    {
        if (Selected != null && !ConfirmPlacement()) RestoreSelection();
        Select(item);
    }

    // Thêm: the storage sheet opens over the tank; a piece picked there is placed in this session.
    public void Add()
    {
        if (!Editing || Selected != null || Brushing || inventory == null) return;
        inventory.Pick();
    }

    private void RestoreSelection()
    {
        if (Selected == null) return;
        if (isNew) Destroy(Selected.gameObject);
        else
        {
            Selected.transform.SetParent(originalParent, true);
            Selected.transform.SetPositionAndRotation(originalPosition, originalRotation);
            Selected.transform.localScale = originalScale;
        }
        Selected = null;
        isNew = false;
    }

    private void CloseEditor()
    {
        if (!Editing) return;
        Editing = false;
        Remember(null, null);
        before = null;
        dock.SetActive(false);
        foreach (var button in new[] { tiltButton, trimButton, growButton, addButton, undoButton, doneButton })
            if (button != null) button.SetActive(false);
        selectionRing.gameObject.SetActive(false);
        // Xong stays tappable under the Thêm sheet; leaving closes the sheet too.
        if (inventory != null && inventory.gameObject.activeInHierarchy) screen.ClosePanels();
        screen.toolbar.SetActive(true);
        viewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        if (reactions != null) reactions.enabled = reactionsEnabled;
        orbit.enabled = true;
    }

    private void Update()
    {
        var pointer = Pointer.current;
        // A drag that starts in the tank keeps moving the piece when the finger runs over the dock,
        // so pieces can be pulled right up to the front glass; a press on a button never moves it.
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            pressStart = pointer.position.ReadValue();
            pressInTank = Editing && !OverUI(pressStart);
            pressOnSelected = pressInTank && Selected != null && HitsSelected(viewCamera.ScreenPointToRay(pressStart));
            dragged = pickedThisPress = false;
        }
        if (!Editing || pointer == null) return;
        // While the storage sheet is open over the tank, taps belong to it.
        if (inventory != null && inventory.gameObject.activeInHierarchy) return;
        // Tapping the selected piece again, without dragging it, places it.
        if (pointer.press.wasReleasedThisFrame && pressInTank && pressOnSelected && !dragged && !pickedThisPress && !Brushing)
        { ConfirmPlacement(); return; }
        if (!pointer.press.isPressed || !pressInTank) return;
        Vector2 position = pointer.position.ReadValue();
        if ((position - pressStart).sqrMagnitude > TapSlop * TapSlop) dragged = true;
        // A second finger is reserved for system gestures, never object placement.
        if (Touchscreen.current != null)
        {
            int pressed = 0;
            foreach (var touch in Touchscreen.current.touches) if (touch.press.isPressed) pressed++;
            if (pressed > 1) return;
        }
        Ray ray = viewCamera.ScreenPointToRay(position);
        if (Brushing)
        {
            if (SurfaceHit(ray, null, out var sand) && carpet.Paint(sand, .1f, erasing ? 0 : brushSpecies, BrushAllowance)) BrushHint();
            return;
        }
        bool pressFrame = pointer.press.wasPressedThisFrame;
        // A press on another piece picks it; it then moves only once the finger travels, and this tap never places it.
        if (pressFrame && Physics.Raycast(ray, out var picked, 30, 1 << 8, QueryTriggerInteraction.Collide))
        {
            var item = picked.collider.GetComponentInParent<AquariumDecorItem>();
            if (item != Selected && items.Contains(item))
            {
                Switch(item);
                pressOnSelected = pickedThisPress = true;
                return;
            }
        }
        // A press on the selected piece moves it only once the finger has travelled, so a tap stays a tap.
        if (pressOnSelected && !dragged) return;
        // A plant dragged over wood or stone attaches to it; never on the press frame, which picks instead.
        if (!pressFrame && Selected != null && Mountable(Selected))
        {
            if (WoodAlongRay(ray, out _) != null) { MoveAlongRay(ray); return; }
            if (Physics.Raycast(ray, out var hit, 30, 1 << 8, QueryTriggerInteraction.Collide))
            {
                var item = hit.collider.GetComponentInParent<AquariumDecorItem>();
                if (item != Selected && Mounts(Selected, item)) { AttachTo(item, hit.point); return; }
            }
        }
        if (Selected != null) MoveAlongRay(ray);
    }

    private void LateUpdate()
    {
        if (Editing) orbit.enabled = false;
    }

    private bool OverUI(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
        return uiHits.Count > 0;
    }

    // Only the nearest piece counts, so a selected piece hidden behind another never swallows the tap.
    private bool HitsSelected(Ray ray) =>
        Physics.Raycast(ray, out var hit, 30, 1 << 8, QueryTriggerInteraction.Collide) &&
        hit.collider.GetComponentInParent<AquariumDecorItem>() == Selected;

    // The dock shows Thêm / Hoàn tác / Xong while nothing is selected, the piece's actions while one is,
    // and the brush's while painting carpet (Xoay hidden).
    private void ShowDock()
    {
        bool idle = Selected == null && !Brushing;
        for (int i = 0; i < 4; i++)
        {
            var action = dock.transform.Find("PlacementAction" + i);
            if (action != null) action.gameObject.SetActive(!idle && !(Brushing && i == 0));
        }
        if (idle) foreach (var button in new[] { tiltButton, trimButton, growButton }) if (button != null) button.SetActive(false);
        foreach (var button in new[] { addButton, undoButton, doneButton }) if (button != null) button.SetActive(idle);
        if (undoButton != null) undoButton.GetComponent<UnityEngine.UI.Button>().interactable = undoLayout != null;
        if (idle) instruction.text = undoLayout != null ? "Chạm một món để chỉnh • Hoàn tác để lùi lần vừa rồi • Xong khi hoàn tất" :
            "Chạm một món để chỉnh • Thêm để lấy đồ trong kho • Xong khi hoàn tất";
    }

    // Nothing selected: the camera looks over the whole tank again.
    private void Idle()
    {
        selectionRing.gameObject.SetActive(false);
        FramePlacementArea();
        ShowDock();
    }

    private void Remember(Layout layout, byte[] carpetCells)
    {
        undoLayout = layout;
        undoCarpet = carpetCells;
    }

    private void RefreshSelection()
    {
        ShowDock();
        if (Selected == null) return;
        bool valid = CanPlace(Selected, Selected.transform.position);
        bool attached = Selected.transform.parent != itemsRoot;
        if (tiltButton != null) tiltButton.SetActive(Selected.tiltable && !attached);
        if (trimButton != null) trimButton.SetActive(Trimmable(Selected));
        if (growButton != null) growButton.SetActive(Trimmable(Selected));
        instruction.text = !valid ? "Vòng đỏ: không còn chỗ trống ở đây. Kéo sang chỗ khác." :
            attached ? "Đã gắn lên lũa/đá • Xoay rồi chọn Đặt" :
            Selected.wallMounted ? "Kéo trên kính sau hồ • Ghép nhiều tấm thành tường rêu • Chọn Đặt" :
            Selected.floats ? "Kéo trên mặt nước • Xoay rồi chọn Đặt" :
            Selected.attachable ? "Kéo trên cát, lên lũa hoặc đá để gắn • Chọn Đặt" :
            Mountable(Selected) ? "Kéo trên cát, sát lũa, hoặc lên đá để gắn • Chọn Đặt" :
            Raised(Selected, Selected.transform.position) ? "Đang chồng lên đá/lũa • Xoay rồi chọn Đặt" :
            Selected.tiltable ? "Kéo trên cát hoặc lên đá • Xoay, Nghiêng rồi chọn Đặt" : "Kéo trên cát hoặc lên đá để chồng • Xoay rồi chọn Đặt";
        selectionRing.gameObject.SetActive(true);
        selectionRing.startColor = selectionRing.endColor = valid ? new Color(.6f, 1, .55f) : new Color(1, .25f, .15f);
        if (Selected.wallMounted)
        {
            // A rectangle on the glass, drawn just in front of the panel.
            selectionRing.transform.position = Selected.transform.position + Vector3.back * .05f;
            Vector2 size = Selected.wallSize;
            for (int i = 0; i < 49; i++)
            {
                float t = (i % 48) / 48f * 2 * (size.x + size.y), x, y;
                if (t < size.x) { x = t; y = 0; }
                else if (t < size.x + size.y) { x = size.x; y = t - size.x; }
                else if (t < 2 * size.x + size.y) { x = 2 * size.x + size.y - t; y = size.y; }
                else { x = 0; y = 2 * (size.x + size.y) - t; }
                selectionRing.SetPosition(i, new Vector3(x - size.x * .5f, y, 0));
            }
            return;
        }
        selectionRing.transform.position = Selected.transform.position + Vector3.up * .012f;
        for (int i = 0; i < 49; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            selectionRing.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Selected.footprint);
        }
    }

    private Layout Capture()
    {
        var layout = new Layout();
        foreach (var item in items)
        {
            var host = item.transform.parent.GetComponent<AquariumDecorItem>();
            Vector3 position = host != null ? item.transform.localPosition : item.transform.position;
            if (string.IsNullOrEmpty(item.instanceId)) item.instanceId = Guid.NewGuid().ToString("N");
            layout.items.Add(new Entry { kind = item.kind, id = item.instanceId, parentId = host != null ? host.instanceId : "",
                x = position.x, y = position.y, z = position.z,
                yaw = host != null ? item.transform.localEulerAngles.y : item.transform.eulerAngles.y,
                tilt = host != null || !item.tiltable ? 0 : Mathf.DeltaAngle(0, item.transform.eulerAngles.z),
                height = Trimmable(item) ? item.transform.localScale.y : 1 });
        }
        return layout;
    }

    private void SaveLayout()
    {
        if (!persistLayout) return;
        PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(Capture()));
        PlayerPrefs.Save();
    }

    private void LoadLayout()
    {
        Layout layout;
        try { layout = JsonUtility.FromJson<Layout>(PlayerPrefs.GetString(storageKey)); }
        catch (ArgumentException) { return; }
        if (!Apply(layout)) return;
        // A save from a larger tank (or from before tank sizes) keeps whatever fits this one, and pieces
        // stacked where the rules no longer allow, such as stone on wood, drop to the next surface down.
        // Settle comes first so a dropped piece has slid free before Fit checks it; Settle again drops
        // whatever Fit moved or put back from under it.
        Settle();
        Fit();
        Settle();
        SaveLayout();
    }

    // Replaces the pieces in the tank with a saved layout; a damaged layout changes nothing.
    private bool Apply(Layout layout)
    {
        if (layout?.items == null) return false;
        var loaded = new List<AquariumDecorItem>();
        var byId = new Dictionary<string, AquariumDecorItem>();
        foreach (var entry in layout.items)
        {
            if (entry == null || entry.kind < 0 || entry.kind >= prefabs.Length || prefabs[entry.kind] == null ||
                !float.IsFinite(entry.x) || !float.IsFinite(entry.y) || !float.IsFinite(entry.z) || !float.IsFinite(entry.yaw) ||
                !float.IsFinite(entry.tilt) || !float.IsFinite(entry.height))
            { Discard(loaded); return false; }
            string id = string.IsNullOrEmpty(entry.id) ? Guid.NewGuid().ToString("N") : entry.id;
            if (byId.ContainsKey(id)) { Discard(loaded); return false; }
            var item = Instantiate(prefabs[entry.kind], itemsRoot);
            item.instanceId = id;
            // Saves from before trimming have no height (0).
            if (Trimmable(item) && entry.height > 0) SetHeight(item, Mathf.Clamp(entry.height, .6f, 1.6f));
            byId.Add(id, item);
            loaded.Add(item);
        }
        // Parents are resolved after all entries, independent of the order in the save.
        for (int i = 0; i < loaded.Count; i++)
        {
            var entry = layout.items[i]; var item = loaded[i];
            if (!string.IsNullOrEmpty(entry.parentId))
            {
                if (!byId.TryGetValue(entry.parentId, out var host) || host == item || !host.supportsPlants ||
                    !DecorPlacementRules.CanMountOn(RoleOf(item), RoleOf(host)) ||
                    !string.IsNullOrEmpty(layout.items[loaded.IndexOf(host)].parentId)) { Discard(loaded); return false; }
                Vector3 local = new Vector3(entry.x, entry.y, entry.z);
                if (host.plantMounts == null || !Array.Exists(host.plantMounts, x => Vector3.Distance(x, local) < .005f))
                { Discard(loaded); return false; }
                item.transform.SetParent(host.transform, false);
                item.transform.localPosition = local;
                item.transform.localRotation = Quaternion.Euler(0, entry.yaw, 0);
            }
            else item.transform.SetPositionAndRotation(Restore(item, new Vector3(entry.x, entry.y, entry.z)),
                Quaternion.Euler(0, entry.yaw, item.tiltable ? Mathf.Clamp(entry.tilt, -30, 30) : 0));
        }
        var previous = new List<AquariumDecorItem>(items);
        items.Clear(); items.AddRange(loaded);
        Discard(previous);
        surfaceCache.Clear();
        return true;
    }

    // After the tank changes: every piece keeps its position, pulled inside the new glass when it falls
    // outside, and the carpet keeps the cells that still lie in the tank.
    public void Refit()
    {
        Settle();
        Fit();
        Settle();
        SaveLayout();
        if (carpet != null) { carpet.Init(this); carpet.Save(); }
    }

    // Pulls each piece on the sand or the back glass inside the placement area, then puts back in stock
    // any piece that still cannot stand there, or that the tank's points no longer cover. Returns
    // whether anything went back to stock.
    private bool Fit()
    {
        bool removed = false;
        int points = 0;
        surfaceCache.Clear();
        foreach (var item in items.ToArray())
        {
            if (item == null || !items.Contains(item)) continue;
            if (item.transform.parent == itemsRoot) item.transform.position = Restore(item, Inside(item, item.transform.position));
            int cost = Mathf.Max(1, item.points);
            if (points + cost <= capacity && CanPlace(item, item.transform.position)) { points += cost; continue; }
            // Plants attached to it go back to stock with it.
            items.RemoveAll(x => x == null || x.transform.IsChildOf(item.transform));
            Destroy(item.gameObject);
            removed = true;
        }
        return removed;
    }

    private Vector3 Inside(AquariumDecorItem item, Vector3 p)
    {
        Vector2 reach = item.wallMounted ? new Vector2(item.wallSize.x * .5f, 0) : Reach(item);
        p.x = Mathf.Clamp(p.x, min.x + reach.x, Mathf.Max(min.x + reach.x, max.x - reach.x));
        if (!item.wallMounted) p.z = Mathf.Clamp(p.z, min.y + reach.y, Mathf.Max(min.y + reach.y, max.y - reach.y));
        return p;
    }

    // A saved position: stacked stone and wood and moss walls keep their height (raised onto the
    // sand if it has since been sloped), anything else rests as when placed.
    private Vector3 Restore(AquariumDecorItem item, Vector3 p) =>
        item.Stacks ? new Vector3(p.x, Mathf.Clamp(p.y, SandAt(p), WallTop), p.z) :
        item.wallMounted ? Rest(item, new Vector3(p.x, Mathf.Max(p.y, SandAt(new Vector3(p.x, 0, WallZ))), p.z)) : Rest(item, p);

    private static void Discard(List<AquariumDecorItem> list)
    {
        foreach (var item in list) if (item != null) Destroy(item.gameObject);
    }
}
