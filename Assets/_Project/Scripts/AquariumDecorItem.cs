using UnityEngine;

public sealed class AquariumDecorItem : MonoBehaviour
{
    public int kind;
    public float footprint = .15f;
    public Transform foliage;
    public float phase;

    private void LateUpdate()
    {
        if (foliage != null)
            foliage.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * .8f + phase) * 2,
                0, Mathf.Sin(Time.time * 1.1f + phase) * 3);
    }
}
