using UnityEngine;

public sealed class AquariumDecorItem : MonoBehaviour
{
    public int kind;
    public float footprint = .15f;
    public Transform foliage;
    public float phase;
    public string instanceId;
    public bool floats;
    public bool attachable;
    public bool supportsPlants;
    public Vector3[] plantMounts = new Vector3[0];
    // Hangs flat on the back glass instead of standing on the sand; the pivot is the bottom centre.
    public bool wallMounted;
    public Vector2 wallSize;
    // Weight against the tank's capacity: larger pieces cost more points.
    public int points = 1;
    // Driftwood can lean sideways with the Nghiêng button.
    public bool tiltable;
    // A carpet species painted with the brush (AquariumCarpet) instead of placed; 0 for everything else.
    public int carpet;

    // Stones, wood and corals: they can rest on top of one another.
    public bool Stacks => !floats && !wallMounted && !attachable && foliage == null;

    private void LateUpdate()
    {
        if (foliage != null)
        {
            float flow = Time.time * .65f + phase + transform.position.x * 2 + transform.position.z * 3;
            foliage.localRotation = Quaternion.Euler(Mathf.Sin(flow) * 1.2f,
                0, Mathf.Sin(flow * 1.17f + .8f) * 1.8f);
        }
    }
}
