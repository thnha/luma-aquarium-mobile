using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Aquarium soundscape in layers, next to AquariumWaterEffects (built by AquariumAudioBuilder):
// - a water bed that swells slightly for a moment whenever the surface ripples,
// - the air-pump stream, following the RisingBubbles emitter (fades out when it stops emitting),
// - while the pump runs, a stream of bubble pops timed like an air stone: at random, in little clusters, its rate
//   slowly breathing up and down; mostly small high "tons", now and then a big low "bloop". Each sounds where a
//   RisingBubbles bubble last burst, and two never start closer than minGap, so they don't pile up,
// - droplet ticks for each ripple from a tap (a moved decoration stays silent); the fish at the surface only stirs the bed,
//   its sound is the gulp BettaMouth plays when it takes air.
// SetMuffled moves the mixer to its "Muffled" snapshot while a shop covers the tank. Shops and fish reach the
// tank's audio through Current, since the environment arrives with the scene rather than wired to them.
public sealed class AquariumAudio : MonoBehaviour
{
    public static AquariumAudio Current { get; private set; }

    public AquariumWaterEffects water;
    public AudioMixer mixer;
    public AudioMixerGroup waterGroup, bubblesGroup, sfxGroup;
    public AudioClip waterLoop, bubbleLoop;
    public AudioClip[] bloops, tons, ripples, gulps;
    [Range(0, 1)] public float waterVolume = 0f, bubbleVolume = 0f, bloopVolume = .25f, rippleVolume = .5f, gulpVolume = .55f;
    public float popRate = 4;                                // average pops per second at full pump
    [Range(0, 1)] public float bloopShare = .15f;             // share of pops from a big bubble
    [Range(0, 1)] public float breathDepth = .6f;             // how far the rate swells and fades
    public float breathSpeed = .2f;                          // how fast it does, in cycles per second
    [Range(0, 1)] public float clusterChance = .35f;          // chance a pop brings one or two quick followers
    public float minGap = .07f;                              // seconds two pops never start closer than
    public float referenceRate = 4.6f;                       // RisingBubbles emission the volumes are tuned for
    public float rippleGap = .4f;                            // shortest time between two ripple sounds

    const int Voices = 6;
    AudioSource waterSource, bubbleSource;
    readonly AudioSource[] voices = new AudioSource[Voices];
    int nextVoice;
    float stir, bubbleLevel, nextRipple, nextPop, breathSeed;
    int followers;
    Vector4 lastBurst = new Vector4(0, 0, 0, -1);   // position and size of the latest burst, w < 0 before any
    ParticleSystem.Particle[] particles;
    Dictionary<uint, Vector4> alive = new Dictionary<uint, Vector4>(), seen = new Dictionary<uint, Vector4>();

    void Awake()
    {
        breathSeed = Random.value * 100;
        waterSource = Source(waterGroup, waterLoop);
        bubbleSource = Source(bubblesGroup, bubbleLoop);
        for (int i = 0; i < Voices; i++) voices[i] = Source(sfxGroup, null);
    }

    void OnEnable()
    {
        Current = this;
        if (water != null) water.Rippled += OnRipple;
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
        if (water != null) water.Rippled -= OnRipple;
        SetMuffled(false, 0);   // snapshots outlive the scene: never leave the next one muffled
    }

    public static void Muffle(bool muffled) { if (Current != null) Current.SetMuffled(muffled); }

    void Update()
    {
        var bubbles = water != null ? water.bubbles : null;
        float rate = bubbles != null && bubbles.isEmitting ? bubbles.emission.rateOverTimeMultiplier / referenceRate : 0;
        bubbleLevel = Mathf.MoveTowards(bubbleLevel, Mathf.Clamp01(rate), Time.deltaTime * .5f);
        bubbleSource.volume = bubbleVolume * bubbleLevel;

        stir = Mathf.MoveTowards(stir, 0, Time.deltaTime * .7f);
        waterSource.volume = waterVolume * (1 + .15f * stir);
        waterSource.pitch = 1 + .01f * stir;

        if (bubbles == null) return;
        HearBursts(bubbles);
        PlayStream(bubbles.main);
    }

    // A bubble missing since last frame has burst: the next pop of a run sounds where it was last seen.
    void HearBursts(ParticleSystem bubbles)
    {
        var main = bubbles.main;
        if (particles == null || particles.Length < main.maxParticles) particles = new ParticleSystem.Particle[main.maxParticles];
        int n = bubbles.GetParticles(particles);
        (alive, seen) = (seen, alive);
        alive.Clear();
        for (int i = 0; i < n; i++)
        {
            Vector3 p = particles[i].position;
            alive[particles[i].randomSeed] = new Vector4(p.x, p.y, p.z, particles[i].startSize);
        }
        foreach (var bubble in seen)
            if (!alive.ContainsKey(bubble.Key)) lastBurst = bubble.Value;
    }

    // Pops arrive at random (exponential gaps) around popRate, scaled by the pump and by a slow noise "breath";
    // a cluster's followers come quicker and softer, as bubbles from one burst of air do.
    void PlayStream(ParticleSystem.MainModule main)
    {
        if (Time.time < nextPop || lastBurst.w < 0) return;
        float breath = 1 + breathDepth * (2 * Mathf.PerlinNoise(breathSeed, Time.time * breathSpeed) - 1);
        float rate = popRate * bubbleLevel * breath;
        if (rate <= .01f) { nextPop = Time.time + .25f; followers = 0; return; }

        bool follower = followers > 0;
        if (follower) followers--;
        else if (Random.value < clusterChance) followers = Random.Range(1, 3);
        bool big = !follower && Random.value < bloopShare;
        float size = Mathf.InverseLerp(main.startSize.constantMin, main.startSize.constantMax, lastBurst.w);
        float volume = bloopVolume * Mathf.Lerp(.55f, 1, size) * (follower ? Random.Range(.35f, .7f) : Random.Range(.6f, 1));
        Play(Pick(big ? bloops : tons), lastBurst, volume, Random.Range(.94f, 1.06f));

        float gap = followers > 0 ? Random.Range(.07f, .14f) : -Mathf.Log(1 - Random.value) / rate;
        nextPop = Time.time + Mathf.Max(minGap, gap);
    }

    public void SetMuffled(bool muffled, float seconds = .35f)
    {
        var snapshot = mixer != null ? mixer.FindSnapshot(muffled ? "Muffled" : "Default") : null;
        if (snapshot != null) snapshot.TransitionTo(seconds);
    }

    // The betta's suction pop as it takes air at the surface.
    public void PlayGulp(Vector3 at) =>
        Play(Pick(gulps), at, gulpVolume * Random.Range(.8f, 1), Random.Range(.92f, 1.08f));

    void OnRipple(Vector3 point, bool byFish)
    {
        stir = Mathf.Max(stir, byFish ? .5f : 1);
        if (byFish) return;
        if (water.decoration != null && water.decoration.Editing) return;   // taps are off while editing: it's a moved decoration
        if (Time.time < nextRipple) return;
        nextRipple = Time.time + rippleGap;
        Play(Pick(ripples), point, rippleVolume * Random.Range(.7f, 1), Random.Range(.9f, 1.1f));
    }

    void Play(AudioClip clip, Vector3 at, float volume, float pitch)
    {
        if (clip == null || volume <= 0) return;   // a silenced layer must not take a voice from the others
        var voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % Voices;
        float edge = water != null && water.decoration != null ? water.decoration.max.x : .9f;
        voice.panStereo = Mathf.Clamp(at.x / edge, -1, 1) * .6f;
        voice.pitch = pitch;
        voice.clip = clip;
        voice.volume = volume;
        voice.Play();
    }

    AudioSource Source(AudioMixerGroup group, AudioClip loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = group;
        source.spatialBlend = 0;
        source.playOnAwake = false;
        if (loop == null) return source;
        source.clip = loop;
        source.loop = true;
        source.time = Random.Range(0, loop.length);   // the two beds never line up the same way twice
        source.Play();
        return source;
    }

    static AudioClip Pick(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
}
