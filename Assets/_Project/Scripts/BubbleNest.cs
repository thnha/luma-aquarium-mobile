using UnityEngine;

// The bubble nest a well-fed male betta builds at the surface (BettaFishAI.State.Nest): a raft of small
// bubbles packed round one spot, a little domed in the middle. It grows a few bubbles per blow and
// thins out over minutes as bubbles pop, so the fish comes back now and then to add to it.
public sealed class BubbleNest : MonoBehaviour
{
    public int maxBubbles = 110;
    public float radius = 0.13f;                   // metres across the full raft, from its middle
    public Vector2 bubbleSize = new Vector2(0.018f, 0.034f);
    [Range(0f, 1f)] public float foam = 1f;         // Aquarium Bubble _Foam: milky nest bubbles, not clear ones
    public Vector2 lifetime = new Vector2(240f, 480f); // seconds before a bubble pops

    static readonly int FoamId = Shader.PropertyToID("_Foam");
    ParticleSystem bubbles;
    Material material;

    public Vector3 Site { get; private set; }
    public bool HasSite { get; private set; }
    public int Count => bubbles ? bubbles.particleCount : 0;
    // 0 empty .. 1 full: how much the fish still wants to add.
    public float Fullness => Mathf.Clamp01((float)Count / Mathf.Max(1, maxBubbles));

    public static BubbleNest Create(Material material)
    {
        var go = new GameObject("BubbleNest");
        go.hideFlags = HideFlags.DontSave;
        var nest = go.AddComponent<BubbleNest>();
        // Its own copy of the bubble material, so only the nest turns milky.
        if (material)
        {
            material = new Material(material) { name = material.name + " (Nest)", hideFlags = HideFlags.DontSave };
            material.SetFloat(FoamId, nest.foam);
            nest.material = material;
        }
        nest.bubbles = CreateBubbles(go, material, nest.maxBubbles);
        return nest;
    }

    // A new site starts a new nest: the old one is abandoned and soon pops.
    public void SetSite(Vector3 site)
    {
        if (HasSite && (site - Site).sqrMagnitude < 0.0004f) return;
        Site = site;
        HasSite = true;
    }

    // One blow: a few bubbles join the raft, the further out the bigger it already is, so it spreads
    // from the middle; nearer the middle they sit a little higher (the dome), and some hang just under
    // the others, so seen from below the raft has some thickness.
    public void Add(int count)
    {
        if (!bubbles || !HasSite) return;
        for (int i = 0; i < count && Count < maxBubbles; i++)
        {
            float reach = radius * Mathf.Sqrt(Mathf.Max(0.05f, Fullness));
            Vector2 offset = Random.insideUnitCircle * reach;
            float size = Random.Range(bubbleSize.x, bubbleSize.y);
            float dome = (1f - offset.magnitude / Mathf.Max(0.001f, radius)) * size * 0.6f;
            var emit = new ParticleSystem.EmitParams
            {
                position = Site + new Vector3(offset.x, Random.Range(-size * 0.6f, dome), offset.y),
                velocity = Vector3.zero,
                startSize = size,
                startLifetime = Random.Range(lifetime.x, lifetime.y),
                applyShapeToPosition = false,
            };
            bubbles.Emit(emit, 1);
        }
    }

    void OnDestroy()
    {
        if (material) Destroy(material);
    }

    static ParticleSystem CreateBubbles(GameObject go, Material material, int max)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.startSpeed = 0f;
        main.startColor = new Color(.91f, .98f, 1f, .8f);
        main.playOnAwake = false;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        // Pops by fading out quickly at the very end.
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.85f, .01f), new GradientAlphaKey(.85f, .99f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }
}
