using UnityEngine;

// Betta pellets float on the surface for a while, then slowly soak and sink,
// and lie on the sand until eaten or they dissolve.
public class FoodPellet : MonoBehaviour
{
    public float fallSpeed = 0.09f;
    public float minY = 0.4f;
    public float lifetime = 90f;                  // safety cap from the drop
    public float bottomLifetime = 20f;            // time on the sand before it dissolves
    public float minFloatTime = 3f;
    public float maxFloatTime = 6f;
    public float bobAmplitude = 0.004f;
    public float sinkWobble = 0.01f;              // sideways sway while sinking, metres
    public float dissolveTime = 1f;

    float floatTimer;
    float sinkSpeed;
    float baseY;
    float phase;
    float age;
    float bottomTimer;
    Vector3 baseScale;
    Vector3 lastWobble;

    // True once it has reached the bottom (or come to rest on a rock).
    public bool OnBottom { get; private set; }
    // Set by the fish when it can't reach it, so it stops going after it.
    public bool Unreachable { get; set; }
    // Height of whatever solid lies under a spot (FeedingSystem passes the fish's decor scan).
    public System.Func<Vector3, float> groundHeight;

    void Start()
    {
        floatTimer = Random.Range(minFloatTime, maxFloatTime);
        baseY = transform.position.y;
        phase = Random.Range(0f, 6.28f);
        baseScale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (OnBottom) bottomTimer += Time.deltaTime;
        float remaining = Mathf.Min(lifetime - age, OnBottom ? bottomLifetime - bottomTimer : float.MaxValue);
        if (remaining <= 0f) { Destroy(gameObject); return; }
        if (remaining < dissolveTime) transform.localScale = baseScale * (remaining / dissolveTime);

        if (floatTimer > 0f)
        {
            floatTimer -= Time.deltaTime;
            Vector3 p = transform.position;
            p.y = baseY + Mathf.Sin(Time.time * 2f + phase) * bobAmplitude;
            transform.position = p;
            return;
        }
        if (OnBottom) return;
        // Ease into the sink instead of dropping at full speed, swaying a little as it goes.
        sinkSpeed = Mathf.MoveTowards(sinkSpeed, fallSpeed, fallSpeed * Time.deltaTime);
        Vector3 wobble = new Vector3(Mathf.Sin(age * 1.7f + phase), 0f, Mathf.Cos(age * 1.3f + phase)) * sinkWobble;
        Vector3 position = transform.position + Vector3.down * sinkSpeed * Time.deltaTime + wobble - lastWobble;
        lastWobble = wobble;
        float ground = minY;
        if (groundHeight != null) ground = Mathf.Max(ground, groundHeight(position) + transform.localScale.y * 0.5f);
        if (position.y <= ground) { position.y = ground; OnBottom = true; }
        transform.position = position;
    }

    public void Consume() { Destroy(gameObject); }
}
