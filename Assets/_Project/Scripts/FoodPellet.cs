using UnityEngine;

public class FoodPellet : MonoBehaviour
{
    public float fallSpeed = 0.08f;
    public float minY = 0.4f;
    public float lifetime = 25f;

    void Start() { Destroy(gameObject, lifetime); }

    void Update()
    {
        if (transform.position.y > minY)
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
    }

    public void Consume() { Destroy(gameObject); }
}
