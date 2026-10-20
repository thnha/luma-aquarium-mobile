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

    public bool Editing { get; private set; }
    public AquariumDecorItem Selected { get; private set; }
    private bool isNew;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private BettaReactions reactions;
    private bool reactionsEnabled;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly List<AquariumDecorItem> items = new List<AquariumDecorItem>();
    public int Count => items.Count;

    [Serializable] private class Layout { public List<Entry> items = new List<Entry>(); }
    [Serializable] private class Entry { public int kind; public float x, z, yaw; }

    private void Start()
    {
        foreach (var item in itemsRoot.GetComponentsInChildren<AquariumDecorItem>()) items.Add(item);
        if (persistLayout && PlayerPrefs.HasKey(storageKey)) LoadLayout();
        dock.SetActive(false);
        selectionRing.gameObject.SetActive(false);
    }

    public void BeginPlant() => Begin(0);
    public void BeginRock() => Begin(1);
    public void BeginWood() => Begin(2);

    public bool Begin(int kind)
    {
        if (kind < 0 || kind >= prefabs.Length || prefabs[kind] == null) return false;
        Cancel();
        OpenEditor();
        if (items.Count >= capacity)
        {
            instruction.text = "Hồ đã đủ 24 món. Chạm món cũ để sửa hoặc xóa.";
            return false;
        }
        Selected = Instantiate(prefabs[kind], itemsRoot);
        Selected.name = prefabs[kind].name;
        Selected.transform.position = new Vector3(0, floorY, -.1f);
        isNew = true;
        RefreshSelection();
        return true;
    }

    public void EditExisting()
    {
        Cancel();
        OpenEditor();
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
        viewCamera.transform.position = new Vector3(0, 2.1f, -3.4f);
        viewCamera.transform.LookAt(new Vector3(0, .9f, 0));
        reactions = screen.fish != null ? screen.fish.GetComponent<BettaReactions>() : null;
        if (reactions != null) { reactionsEnabled = reactions.enabled; reactions.enabled = false; }
    }

    public void Select(AquariumDecorItem item)
    {
        if (!Editing || item == null || !items.Contains(item)) return;
        RestoreSelection();
        Selected = item;
        originalPosition = item.transform.position;
        originalRotation = item.transform.rotation;
        isNew = false;
        RefreshSelection();
    }

    public bool MoveTo(Vector3 position)
    {
        if (!Editing || Selected == null || !float.IsFinite(position.x) || !float.IsFinite(position.z)) return false;
        Selected.transform.position = new Vector3(position.x, floorY, position.z);
        RefreshSelection();
        return CanPlace(Selected, Selected.transform.position);
    }

    public bool CanPlace(AquariumDecorItem item, Vector3 p)
    {
        float r = item.footprint;
        if (!float.IsFinite(p.x) || !float.IsFinite(p.z) || p.x - r < min.x || p.x + r > max.x ||
            p.z - r < min.y || p.z + r > max.y) return false;
        foreach (var other in items)
        {
            if (other == null || other == item) continue;
            Vector2 delta = new Vector2(p.x - other.transform.position.x, p.z - other.transform.position.z);
            if (delta.sqrMagnitude < Mathf.Pow(r + other.footprint, 2)) return false;
        }
        return true;
    }

    public void Rotate()
    {
        if (Selected == null) return;
        Selected.transform.Rotate(0, 30, 0, Space.World);
        RefreshSelection();
    }

    public bool ConfirmPlacement()
    {
        if (Selected == null || !CanPlace(Selected, Selected.transform.position)) return false;
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
        if (!isNew) items.Remove(Selected);
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
        else Selected.transform.SetPositionAndRotation(originalPosition, originalRotation);
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
        if (pointer.press.wasPressedThisFrame && Physics.Raycast(ray, out var hit, 30, 1 << 8, QueryTriggerInteraction.Collide))
        {
            var item = hit.collider.GetComponentInParent<AquariumDecorItem>();
            if (item != Selected && items.Contains(item)) Select(item);
        }
        else if (Selected != null && new Plane(Vector3.up, new Vector3(0, floorY, 0)).Raycast(ray, out float distance))
            MoveTo(ray.GetPoint(distance));
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
        instruction.text = valid ? "Kéo trên nền cát • Xoay rồi chọn Đặt" : "Vòng đỏ: ra ngoài hồ hoặc chồng đồ. Kéo sang chỗ trống.";
        selectionRing.gameObject.SetActive(true);
        selectionRing.transform.position = Selected.transform.position + Vector3.up * .012f;
        selectionRing.startColor = selectionRing.endColor = valid ? new Color(.6f, 1, .55f) : new Color(1, .25f, .15f);
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
            layout.items.Add(new Entry { kind = item.kind, x = item.transform.position.x, z = item.transform.position.z, yaw = item.transform.eulerAngles.y });
        PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(layout));
        PlayerPrefs.Save();
    }

    private void LoadLayout()
    {
        Layout layout;
        try { layout = JsonUtility.FromJson<Layout>(PlayerPrefs.GetString(storageKey)); }
        catch (ArgumentException) { return; }
        if (layout?.items == null || layout.items.Count > capacity) return;
        // Validate the whole file before replacing the default arrangement.
        var loaded = new List<AquariumDecorItem>();
        foreach (var entry in layout.items)
        {
            if (entry == null || entry.kind < 0 || entry.kind >= prefabs.Length || !float.IsFinite(entry.x) ||
                !float.IsFinite(entry.z) || !float.IsFinite(entry.yaw)) { Discard(loaded); return; }
            var prefab = prefabs[entry.kind];
            float r = prefab.footprint;
            if (entry.x - r < min.x || entry.x + r > max.x || entry.z - r < min.y || entry.z + r > max.y)
            { Discard(loaded); return; }
            foreach (var previous in loaded)
                if (Vector2.Distance(new Vector2(entry.x, entry.z), new Vector2(previous.transform.position.x, previous.transform.position.z)) < r + previous.footprint)
                { Discard(loaded); return; }
            var item = Instantiate(prefab, itemsRoot);
            item.transform.SetPositionAndRotation(new Vector3(entry.x, floorY, entry.z), Quaternion.Euler(0, entry.yaw, 0));
            loaded.Add(item);
        }
        Discard(items);
        items.Clear();
        items.AddRange(loaded);
    }

    private static void Discard(List<AquariumDecorItem> list)
    {
        foreach (var item in list) if (item != null) Destroy(item.gameObject);
    }
}
