using System.Collections.Generic;
using UnityEngine;

public class FeedingSystem : MonoBehaviour
{
    public BettaFishAI fish;
    public FoodPellet pelletPrefab;
    public Transform spawnPoint;
    public float feedCooldown = 1f;
    public int maxActivePellets = 3;
    public float spawnJitter = 0.15f;

    private readonly List<FoodPellet> pellets = new List<FoodPellet>();
    private float nextFeedTime;

    public void Feed()
    {
        pellets.RemoveAll(p => p == null);
        if (Time.time < nextFeedTime || pelletPrefab == null || spawnPoint == null) return;
        if (pellets.Count >= maxActivePellets) return;
        nextFeedTime = Time.time + feedCooldown;

        Vector2 jitter = Random.insideUnitCircle * spawnJitter;
        Vector3 position = spawnPoint.position + new Vector3(jitter.x, 0f, jitter.y);
        FoodPellet pellet = Instantiate(pelletPrefab, position, Quaternion.identity);
        pellets.Add(pellet);
        if (fish != null) fish.Seek(NearestPellet(fish.transform.position));
    }

    public FoodPellet NearestPellet(Vector3 from)
    {
        FoodPellet best = null;
        float bestDistance = float.MaxValue;
        for (int i = pellets.Count - 1; i >= 0; i--)
        {
            FoodPellet p = pellets[i];
            if (p == null) { pellets.RemoveAt(i); continue; }
            float d = (p.transform.position - from).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = p; }
        }
        return best;
    }
}
