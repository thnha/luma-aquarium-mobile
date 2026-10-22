using UnityEngine;

// Soft shafts of sunlight slanting down from the surface to the sand. Each shaft is one quad that
// turns about its own slant to face the camera; the Aquarium Glow shader fades them out at night.
public sealed class AquariumSunShafts : MonoBehaviour
{
    public Material material;
    public AquariumDecoration decoration;
    public AquariumWaterEffects water;
    public int count = 7;
    // Degrees from vertical; the shafts lean down toward +x, away from the sun.
    public float slant = 16;
    public int seed = 5;
    private Transform[] shafts;
    private Vector3 axis;

    private void Start() => Rebuild();

    // Spreads the shafts over the placement area again, as after the tank changes size (AquariumTank).
    public void Rebuild()
    {
        OnDestroy();
        foreach (Transform child in transform) Destroy(child.gameObject);
        shafts = null;
        if (material == null || decoration == null) return;
        float top = water != null ? water.surfaceY : decoration.floorY + 2.1f;
        float length = (top - decoration.floorY) / Mathf.Cos(slant * Mathf.Deg2Rad);
        axis = Quaternion.Euler(0, 0, slant) * Vector3.up;
        var random = new System.Random(seed);
        shafts = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            float width = .1f + (float)random.NextDouble() * .2f;
            // Spread across the tank, starting left of it so the slant carries the light over the sand.
            float x = Mathf.Lerp(decoration.min.x - .3f, decoration.max.x, (i + (float)random.NextDouble()) / count);
            float z = Mathf.Lerp(decoration.min.y + .5f, decoration.max.y - .1f, (float)random.NextDouble());
            var shaft = new GameObject("Shaft" + i, typeof(MeshFilter), typeof(MeshRenderer));
            shaft.transform.SetParent(transform, false);
            shaft.transform.position = new Vector3(x, top, z);
            shaft.GetComponent<MeshFilter>().sharedMesh = Quad(width, length, .6f + .4f * (float)random.NextDouble(), (float)random.NextDouble());
            var renderer = shaft.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            shafts[i] = shaft.transform;
        }
    }

    // Hangs below its pivot: uv.y is 1 at the surface and 0 on the sand; alpha carries the shimmer phase.
    private static Mesh Quad(float width, float length, float brightness, float phase)
    {
        var mesh = new Mesh { name = "SunShaft" };
        mesh.vertices = new[] { new Vector3(-width / 2, -length, 0), new Vector3(width / 2, -length, 0), new Vector3(-width / 2, 0, 0), new Vector3(width / 2, 0, 0) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        var color = new Color(brightness, brightness, brightness, phase);
        mesh.colors = new[] { color, color, color, color };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.bounds = new Bounds(new Vector3(0, -length / 2, 0), new Vector3(length, length, length));
        return mesh;
    }

    private void LateUpdate()
    {
        var camera = decoration != null ? decoration.viewCamera : null;
        if (shafts == null || camera == null) return;
        foreach (var shaft in shafts)
        {
            Vector3 facing = Vector3.ProjectOnPlane(camera.transform.position - shaft.position, axis);
            if (facing.sqrMagnitude > 1e-6f) shaft.rotation = Quaternion.LookRotation(facing, axis);
        }
    }

    private void OnDestroy()
    {
        if (shafts == null) return;
        foreach (var shaft in shafts) if (shaft != null) Destroy(shaft.GetComponent<MeshFilter>().sharedMesh);
    }
}
