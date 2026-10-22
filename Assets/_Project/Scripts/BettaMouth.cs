using UnityEngine;

// Mouth life of a pipeline fish (on its model prefab, next to BettaReactions):
// - yawns now and then while idling or resting (Yawn clip),
// - lets bubbles out of the mouth when BettaFishAI gulps air or blows bubbles at the surface, on the
//   frames where the Gulp / Bubble clips press the lips shut (betta-splendens/pipeline/mouth_clips.py).
// Bubbles rise to the surface and float there for a while, like the start of a bubble nest.
public sealed class BettaMouth : MonoBehaviour
{
    public Transform skeletonRoot;
    public Material bubbleMaterial;
    public float yawnMinInterval = 20f;           // seconds spent idling or resting between yawns
    public float yawnMaxInterval = 50f;
    public Vector2 bubbleSize = new Vector2(0.012f, 0.022f);
    public float bubbleRiseSpeed = 0.22f;         // m/s
    public Vector2 surfaceFloatTime = new Vector2(4f, 9f);

    static readonly int YawnId = Animator.StringToHash("Yawn");
    static readonly float[] GulpBubbleTimes = { 9f / 24f };
    static readonly float[] BlowBubbleTimes = { 8f / 24f, 17f / 24f };
    const int MaxBubbles = 32;

    BettaFishAI fish;
    Animator animator;
    Transform head, jaw;
    ParticleSystem bubbles;
    readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[MaxBubbles];
    float yawnTimer;
    float[] pending = System.Array.Empty<float>();
    int pendingCount;
    float pendingClock;

    void Awake()
    {
        if (skeletonRoot == null) skeletonRoot = transform;
        animator = skeletonRoot.GetComponentInChildren<Animator>();
        head = FindDeep(skeletonRoot, "Head");
        jaw = FindDeep(skeletonRoot, "Jaw_Lower");
        yawnTimer = Random.Range(yawnMinInterval, yawnMaxInterval);
        bubbles = CreateBubbles();
    }

    void OnEnable()
    {
        fish = GetComponentInParent<BettaFishAI>();
        if (!fish) return;
        fish.Gulped += OnGulp;
        fish.BlowingBubbles += OnBlow;
    }

    void OnDisable()
    {
        if (!fish) return;
        fish.Gulped -= OnGulp;
        fish.BlowingBubbles -= OnBlow;
    }

    void OnDestroy()
    {
        if (bubbles) Destroy(bubbles.gameObject);
    }

    void OnGulp() => Schedule(GulpBubbleTimes);
    void OnBlow() => Schedule(BlowBubbleTimes);

    void Schedule(float[] times)
    {
        pending = times;
        pendingCount = 0;
        pendingClock = 0f;
    }

    void Update()
    {
        if (fish && animator && (fish.state == BettaFishAI.State.Idle || fish.IsResting))
        {
            yawnTimer -= Time.deltaTime;
            if (yawnTimer <= 0f)
            {
                animator.SetTrigger(YawnId);
                yawnTimer = Random.Range(yawnMinInterval, yawnMaxInterval);
            }
        }

        pendingClock += Time.deltaTime;
        while (pendingCount < pending.Length && pendingClock >= pending[pendingCount])
        {
            EmitBubble();
            pendingCount++;
        }
    }

    // Bubbles that reach the surface stop there, drift a little and pop when their time is up.
    void LateUpdate()
    {
        if (!bubbles || !fish || bubbles.particleCount == 0) return;
        float surface = fish.waterSurfaceY;
        int n = bubbles.GetParticles(particles);
        for (int i = 0; i < n; i++)
        {
            if (particles[i].position.y < surface) continue;
            Vector3 p = particles[i].position;
            p.y = surface;
            particles[i].position = p;
            particles[i].velocity = new Vector3(particles[i].velocity.x * 0.98f, 0f, particles[i].velocity.z * 0.98f);
        }
        bubbles.SetParticles(particles, n);
    }

    void EmitBubble()
    {
        if (!bubbles || jaw == null) return;
        // Jaw_Lower's pivot sits at the mouth corners; the lips are about a third of the way
        // further out along the head (gills -> corners).
        Vector3 mouth = head ? jaw.position + (jaw.position - head.position) * 0.35f : jaw.position;
        float surface = fish ? fish.waterSurfaceY : mouth.y + 1f;
        float rise = Mathf.Max(0f, surface - mouth.y) / bubbleRiseSpeed;
        var emit = new ParticleSystem.EmitParams
        {
            position = mouth,
            velocity = new Vector3(Random.Range(-0.01f, 0.01f), bubbleRiseSpeed, Random.Range(-0.01f, 0.01f)),
            startSize = Random.Range(bubbleSize.x, bubbleSize.y),
            startLifetime = rise + Random.Range(surfaceFloatTime.x, surfaceFloatTime.y),
            applyShapeToPosition = false,
        };
        bubbles.Emit(emit, 1);
    }

    ParticleSystem CreateBubbles()
    {
        var go = new GameObject("MouthBubbles");   // unparented: bubbles leave the fish behind
        go.hideFlags = HideFlags.DontSave;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxBubbles;
        main.startSpeed = 0f;
        main.startColor = new Color(.91f, .98f, 1f, .85f);
        main.playOnAwake = false;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var noise = ps.noise; noise.enabled = true; noise.separateAxes = true;
        noise.strengthX = .01f; noise.strengthY = 0f; noise.strengthZ = .008f;
        noise.frequency = .6f; noise.scrollSpeed = .2f; noise.quality = ParticleSystemNoiseQuality.Low;
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(.85f, .9f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = bubbleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }

    static Transform FindDeep(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, boneName);
            if (found) return found;
        }
        return null;
    }
}
