using UnityEngine;

// Betta pellets float on the surface for a while, then slowly soak and sink.
public class FoodPellet : MonoBehaviour
{
    public float fallSpeed = 0.04f;
    public float minY = 0.4f;
    public float lifetime = 25f;
    public float minFloatTime = 4f;
    public float maxFloatTime = 9f;
    public float bobAmplitude = 0.004f;

    float floatTimer;
    float sinkSpeed;
    float baseY;
    float phase;

    void Start()
    {
        Destroy(gameObject, lifetime);
        floatTimer = Random.Range(minFloatTime, maxFloatTime);
        baseY = transform.position.y;
        phase = Random.Range(0f, 6.28f);
    }

    void Update()
    {
        if (floatTimer > 0f)
        {
            floatTimer -= Time.deltaTime;
            Vector3 p = transform.position;
            p.y = baseY + Mathf.Sin(Time.time * 2f + phase) * bobAmplitude;
            transform.position = p;
            return;
        }
        // Ease into the sink instead of dropping at full speed.
        sinkSpeed = Mathf.MoveTowards(sinkSpeed, fallSpeed, fallSpeed * Time.deltaTime);
        if (transform.position.y > minY)
            transform.position += Vector3.down * sinkSpeed * Time.deltaTime;
    }

    public void Consume() { Destroy(gameObject); }
}
