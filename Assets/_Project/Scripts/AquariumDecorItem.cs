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
        {
            float flow = Time.time * .65f + phase + transform.position.x * 2 + transform.position.z * 3;
            foliage.localRotation = Quaternion.Euler(Mathf.Sin(flow) * 1.2f,
                0, Mathf.Sin(flow * 1.17f + .8f) * 1.8f);
        }
    }
}
