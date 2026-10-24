using UnityEngine;

// Betta pellets float on the surface for a while, then slowly soak and sink,
// and lie on the sand until eaten or they dissolve.
public class FoodPellet : MonoBehaviour
{
    public float fallSpeed = 0.09f;
    public float minY = 0.4f;
    public float lifetime = 180f;                 // safety cap from the drop
    public float bottomLifetime = 60f;            // time on the sand before it dissolves
    public float minFloatTime = 3f;
    public float maxFloatTime = 6f;
    public float bobAmplitude = 0.004f;
    public float sinkWobble = 0.01f;              // sideways sway while sinking, metres
    public float dissolveTime = 1f;
    public float unreachableRetryTime = 8f;       // the fish tries again this long after giving up on it

    float floatTimer;
    float sinkSpeed;
    float baseY;
    float phase;
    float age;
    float bottomTimer;
    Vector3 baseScale;
    Vector3 lastWobble;
    float unreachableUntil;

    // True once it has reached the bottom (or come to rest on a rock).
    public bool OnBottom { get; private set; }
    // Set by the fish when it can't reach it, so it leaves it be for a while; it is cleared
    // once the pellet settles somewhere new, so a pellet given up on near the glass isn't ignored for good.
    public bool Unreachable
    {
        get => Time.time < unreachableUntil;
        set => unreachableUntil = value ? Time.time + unreachableRetryTime : 0f;
    }
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
        if (position.y <= ground) { position.y = ground; OnBottom = true; Unreachable = false; }
        transform.position = position;
    }

    public void Consume() { Destroy(gameObject); }

    // Taken and spat out again (bettas often do, before swallowing): it lands a little way off,
    // at the same height while it floats or lies on the bottom; once is enough.
    public bool Spat { get; private set; }

    public void Spit(Vector3 position)
    {
        Spat = true;
        Vector3 p = transform.position;
        p.x = position.x;
        p.z = position.z;
        if (!OnBottom && floatTimer <= 0f) p.y = Mathf.Max(p.y, position.y);
        transform.position = p;
        lastWobble = Vector3.zero;
    }
}
