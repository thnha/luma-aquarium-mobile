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
    public string inventoryStorageKey = Lumaquarium.UI.FishShopInventory.DefaultStorageKey;
    public Transform itemsRoot;
    public Camera viewCamera;
    public OrbitTouchCamera orbit;
    public Lumaquarium.UI.MainAquariumScreen screen;
    public GameObject dock;
    public TMP_Text instruction;
    public LineRenderer selectionRing;
    public AquariumWaterEffects water;
    public Vector2 min = new Vector2(-.74f, -.5f);
    public Vector2 max = new Vector2(.74f, .55f);
    public float floorY = .31f;
    public int capacity = 24;
    public string storageKey = "Lumaquarium.Decoration.Preview.v1";
    public bool persistLayout = true;
    // Placement looks down on the sand at this angle, so the back of the tank gets as much screen as the front.
    public float placementPitch = 50;

    public bool Editing { get; private set; }
    public AquariumDecorItem Selected { get; private set; }
    private bool isNew;
    private bool returnToInventory;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private BettaReactions reactions;
    private bool reactionsEnabled;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly List<AquariumDecorItem> items = new List<AquariumDecorItem>();
    public int Count => items.Count;

    [Serializable] private class Layout { public List<Entry> items = new List<Entry>(); }
    [Serializable] private class Entry
    {
        public int kind;
        public float x, y, z, yaw;
        public string id, parentId;
    }

    public int Available(int kind)
    {
        var offer = catalog != null ? catalog.Find(kind) : null;
        if (offer == null) return catalog == null && kind < 3 ? capacity : 0;
        var inventory = new Lumaquarium.UI.FishShopInventory(inventoryStorageKey);
        return Mathf.Max(0, inventory.DecorCount(offer.id) - items.FindAll(x => x != null && x.kind == kind).Count);
    }

    private float Height(AquariumDecorItem item) => item.floats && water != null ? water.surfaceY - .012f : floorY;
    // The back glass is the far edge of the placement area; moss walls cover it up to below the surface.
    private float WallZ => max.y;
    private float WallTop => water != null ? water.surfaceY - .08f : floorY + 1.8f;

    // Where an item comes to rest for a dragged point: on the sand, the surface or the back glass.
    private Vector3 Rest(AquariumDecorItem item, Vector3 p) =>
        item.wallMounted ? new Vector3(p.x, p.y, WallZ) : new Vector3(p.x, Height(item), p.z);

    private void Start()
    {
        foreach (var item in itemsRoot.GetComponentsInChildren<AquariumDecorItem>())
        {
            if (string.IsNullOrEmpty(item.instanceId)) item.instanceId = Guid.NewGuid().ToString("N");
            items.Add(item);
        }
        if (persistLayout && PlayerPrefs.HasKey(storageKey)) LoadLayout();
        if (catalog != null)
        {
            var starter = new Dictionary<string, int>();
            foreach (var item in items)
            {
                var offer = catalog.Find(item.kind);
                if (item.kind >= 3 || offer == null) continue;
                starter[offer.id] = starter.TryGetValue(offer.id, out int count) ? count + 1 : 1;
            }
            new Lumaquarium.UI.FishShopInventory(inventoryStorageKey).ImportStarterDecor(starter);
        }
        dock.SetActive(false);
        selectionRing.gameObject.SetActive(false);
    }

    public void BeginPlant() => OpenShop(0);
    public void BeginRock() => OpenShop(2);
    public void BeginWood() => OpenShop(1);

    private void OpenShop(int category)
    {
        var shop = FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>();
        if (shop == null) { Begin(category == 0 ? 0 : category == 2 ? 1 : 2); return; }
        shop.Open();
        shop.SelectCategory(category);
    }

    public bool Begin(int kind)
    {
        if (kind < 0 || kind >= prefabs.Length || prefabs[kind] == null) return false;
        returnToInventory = false;
        Cancel();
        if (Available(kind) <= 0) return false;
        OpenEditor();
        // Placing from the Trang trí sheet returns to it, so the next item is one tap away.
        returnToInventory = true;
        if (items.Count >= capacity)
        {
            instruction.text = $"Hồ đã đủ {capacity} món. Cất bớt đồ để đặt món mới.";
            return false;
        }
        Selected = Instantiate(prefabs[kind], itemsRoot);
        Selected.name = prefabs[kind].name;
        Selected.instanceId = Guid.NewGuid().ToString("N");
        Selected.transform.position = Rest(Selected, new Vector3(0, floorY + .3f, -.1f));
        FrameSelection();
        isNew = true;
        RefreshSelection();
        return true;
    }

    public void EditExisting()
    {
        Cancel();
        OpenEditor();
        returnToInventory = true;
        instruction.text = "Chạm một món trong hồ để chọn và di chuyển.";
    }

    private void OpenEditor()
    {
        cameraPosition = viewCamera.transform.position;
        cameraRotation = viewCamera.transform.rotation;
        Editing = true;
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
        Selected = item;
        originalParent = item.transform.parent;
        originalPosition = item.transform.position;
        originalRotation = item.transform.rotation;
        isNew = false;
        FrameSelection();
        RefreshSelection();
    }

    private void FrameSelection()
    {
        if (Selected == null) return;
        if (Selected.floats)
        {
            viewCamera.transform.position = new Vector3(0, Height(Selected) + 2f, -2.5f);
            viewCamera.transform.LookAt(new Vector3(0, Height(Selected), 0));
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
            Vector3 back = viewCamera.WorldToViewportPoint(new Vector3(min.x, floorY, max.y));
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
            if (candidate == null || candidate == Selected || !candidate.supportsPlants ||
                !items.Contains(candidate) || hit.distance >= nearest) continue;
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
        if (Selected.attachable)
        {
            var wood = WoodAlongRay(ray, out var hitPoint);
            if (wood != null) return AttachTo(wood, hitPoint);
        }
        return new Plane(Vector3.up, new Vector3(0, Height(Selected), 0)).Raycast(ray, out float distance)
            && MoveTo(ray.GetPoint(distance));
    }

    public bool MoveTo(Vector3 position)
    {
        if (!Editing || Selected == null || !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return false;
        Selected.transform.SetParent(itemsRoot, true);
        Selected.transform.position = Rest(Selected, position);
        RefreshSelection();
        return CanPlace(Selected, Selected.transform.position);
    }

    public bool AttachTo(AquariumDecorItem wood, Vector3 worldPosition)
    {
        if (!Editing || Selected == null || !Selected.attachable || wood == null || !wood.supportsPlants || !items.Contains(wood)) return false;
        if (wood.plantMounts == null || wood.plantMounts.Length == 0) return false;
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
            if (!item.attachable || !host.supportsPlants || !items.Contains(host)) return false;
            foreach (var other in items)
                if (other != null && other != item && other.transform.parent == host.transform &&
                    Vector3.Distance(p, other.transform.position) < .045f) return false;
            return true;
        }
        if (item.wallMounted)
        {
            Vector2 half = item.wallSize * .5f;
            if (p.x - half.x < min.x || p.x + half.x > max.x || p.y < floorY - .001f || p.y + item.wallSize.y > WallTop) return false;
            // Neighbouring panels may overlap a little so the moss closes the seam between them.
            foreach (var other in items)
                if (other != null && other != item && other.wallMounted && other.transform.parent == itemsRoot &&
                    Mathf.Abs(p.x - other.transform.position.x) < half.x + other.wallSize.x * .5f - .03f &&
                    Mathf.Abs(p.y + half.y - other.transform.position.y - other.wallSize.y * .5f) < half.y + other.wallSize.y * .5f - .03f)
                    return false;
            return true;
        }
        float r = item.footprint;
        if (p.x - r < min.x || p.x + r > max.x || p.z - r < min.y || p.z + r > max.y) return false;
        foreach (var other in items)
        {
            if (other == null || other == item || other.transform.IsChildOf(item.transform) ||
                other.transform.parent != itemsRoot || other.floats != item.floats || other.wallMounted) continue;
            Vector2 delta = new Vector2(p.x - other.transform.position.x, p.z - other.transform.position.z);
            if (delta.sqrMagnitude < Mathf.Pow(r + other.footprint, 2)) return false;
        }
        return true;
    }

    public void Rotate()
    {
        if (Selected == null || Selected.wallMounted) return;
        Selected.transform.Rotate(0, 30, 0, Space.World);
        RefreshSelection();
    }

    public bool ConfirmPlacement()
    {
        if (Selected == null || !CanPlace(Selected, Selected.transform.position)) return false;
        if (isNew && (items.Count >= capacity || Available(Selected.kind) <= 0)) return false;
        if (isNew) items.Add(Selected);
        water?.Ripple(Selected.transform.position);
        Selected = null;
        isNew = false;
        SaveLayout();
        CloseEditor();
        return true;
    }

    public void Confirm() => ConfirmPlacement();

    public void Delete()
    {
        if (Selected == null) return;
        if (!isNew)
            items.RemoveAll(x => x == Selected || (x != null && x.transform.IsChildOf(Selected.transform)));
        Destroy(Selected.gameObject);
        Selected = null;
        isNew = false;
        SaveLayout();
        CloseEditor();
    }

    public void Cancel()
    {
        RestoreSelection();
        CloseEditor();
    }

    private void RestoreSelection()
    {
        if (Selected == null) return;
        if (isNew) Destroy(Selected.gameObject);
        else
        {
            Selected.transform.SetParent(originalParent, true);
            Selected.transform.SetPositionAndRotation(originalPosition, originalRotation);
        }
        Selected = null;
        isNew = false;
    }

    private void CloseEditor()
    {
        if (!Editing) return;
        Editing = false;
        dock.SetActive(false);
        selectionRing.gameObject.SetActive(false);
        screen.toolbar.SetActive(true);
        viewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        if (reactions != null) reactions.enabled = reactionsEnabled;
        orbit.enabled = true;
        if (returnToInventory)
        {
            returnToInventory = false;
            screen.OpenDecorate();
        }
    }

    private void Update()
    {
        if (!Editing) return;
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.isPressed) return;
        Vector2 position = pointer.position.ReadValue();
        if (OverUI(position)) return;
        // A second finger is reserved for system gestures, never object placement.
        if (Touchscreen.current != null)
        {
            int pressed = 0;
            foreach (var touch in Touchscreen.current.touches) if (touch.press.isPressed) pressed++;
            if (pressed > 1) return;
        }
        Ray ray = viewCamera.ScreenPointToRay(position);
        if (Selected != null && Selected.attachable && WoodAlongRay(ray, out _) != null)
        { MoveAlongRay(ray); return; }
        if (Physics.Raycast(ray, out var hit, 30, 1 << 8, QueryTriggerInteraction.Collide))
        {
            var item = hit.collider.GetComponentInParent<AquariumDecorItem>();
            // A plant held above wood attaches instead of selecting the wood underneath it.
            if (Selected != null && Selected.attachable && item != Selected && item != null && item.supportsPlants)
            { AttachTo(item, hit.point); return; }
            if (pointer.press.wasPressedThisFrame && item != Selected && items.Contains(item))
            { Select(item); return; }
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

    private void RefreshSelection()
    {
        if (Selected == null) return;
        bool valid = CanPlace(Selected, Selected.transform.position);
        bool attached = Selected.transform.parent != itemsRoot;
        instruction.text = !valid ? "Vòng đỏ: ra ngoài hồ hoặc chồng đồ. Kéo sang chỗ trống." :
            attached ? "Đã gắn lên lũa/đá • Xoay rồi chọn Đặt" :
            Selected.wallMounted ? "Kéo trên kính sau hồ • Ghép nhiều tấm thành tường rêu • Chọn Đặt" :
            Selected.floats ? "Kéo trên mặt nước • Xoay rồi chọn Đặt" :
            Selected.attachable ? "Kéo trên cát, lên lũa hoặc đá để gắn • Chọn Đặt" : "Kéo trên nền cát • Xoay rồi chọn Đặt";
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

    private void SaveLayout()
    {
        if (!persistLayout) return;
        var layout = new Layout();
        foreach (var item in items)
        {
            var host = item.transform.parent.GetComponent<AquariumDecorItem>();
            Vector3 position = host != null ? item.transform.localPosition : item.transform.position;
            if (string.IsNullOrEmpty(item.instanceId)) item.instanceId = Guid.NewGuid().ToString("N");
            layout.items.Add(new Entry { kind = item.kind, id = item.instanceId, parentId = host != null ? host.instanceId : "",
                x = position.x, y = position.y, z = position.z,
                yaw = host != null ? item.transform.localEulerAngles.y : item.transform.eulerAngles.y });
        }
        PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(layout));
        PlayerPrefs.Save();
    }

    private void LoadLayout()
    {
        Layout layout;
        try { layout = JsonUtility.FromJson<Layout>(PlayerPrefs.GetString(storageKey)); }
        catch (ArgumentException) { return; }
        if (layout?.items == null || layout.items.Count > capacity) return;
        var loaded = new List<AquariumDecorItem>();
        var byId = new Dictionary<string, AquariumDecorItem>();
        foreach (var entry in layout.items)
        {
            if (entry == null || entry.kind < 0 || entry.kind >= prefabs.Length || prefabs[entry.kind] == null ||
                !float.IsFinite(entry.x) || !float.IsFinite(entry.y) || !float.IsFinite(entry.z) || !float.IsFinite(entry.yaw))
            { Discard(loaded); return; }
            string id = string.IsNullOrEmpty(entry.id) ? Guid.NewGuid().ToString("N") : entry.id;
            if (byId.ContainsKey(id)) { Discard(loaded); return; }
            var item = Instantiate(prefabs[entry.kind], itemsRoot);
            item.instanceId = id;
            byId.Add(id, item);
            loaded.Add(item);
        }
        // Parents are resolved after all entries, independent of the order in the save.
        for (int i = 0; i < loaded.Count; i++)
        {
            var entry = layout.items[i]; var item = loaded[i];
            if (!string.IsNullOrEmpty(entry.parentId))
            {
                if (!byId.TryGetValue(entry.parentId, out var host) || host == item || !item.attachable || !host.supportsPlants ||
                    !string.IsNullOrEmpty(layout.items[loaded.IndexOf(host)].parentId)) { Discard(loaded); return; }
                Vector3 local = new Vector3(entry.x, entry.y, entry.z);
                if (host.plantMounts == null || !Array.Exists(host.plantMounts, x => Vector3.Distance(x, local) < .005f))
                { Discard(loaded); return; }
                item.transform.SetParent(host.transform, false);
                item.transform.localPosition = local;
                item.transform.localRotation = Quaternion.Euler(0, entry.yaw, 0);
            }
            else item.transform.SetPositionAndRotation(Rest(item, new Vector3(entry.x, entry.y, entry.z)), Quaternion.Euler(0, entry.yaw, 0));
        }
        // Validate against the candidate layout, while keeping the current layout recoverable.
        var previous = new List<AquariumDecorItem>(items);
        items.Clear(); items.AddRange(loaded);
        foreach (var item in loaded)
            if (!CanPlace(item, item.transform.position))
            { items.Clear(); items.AddRange(previous); Discard(loaded); return; }
        Discard(previous);
    }

    private static void Discard(List<AquariumDecorItem> list)
    {
        foreach (var item in list) if (item != null) Destroy(item.gameObject);
    }
}
