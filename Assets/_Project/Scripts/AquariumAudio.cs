using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Aquarium soundscape in layers, next to AquariumWaterEffects (built by AquariumAudioBuilder):
// - a water bed that swells slightly for a moment whenever the surface ripples,
// - the air-pump stream, following the RisingBubbles emitter (fades out when it stops emitting),
// - a pop for some of the RisingBubbles bubbles as they burst under the surface, where they burst, deeper for
//   bigger bubbles, so what you hear matches what you see,
// - droplet ticks for each ripple from a tap or a moved decoration; the fish at the surface only stirs the bed,
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
    public AudioClip[] bloops, ripples, gulps;
    [Range(0, 1)] public float waterVolume = .1f, bubbleVolume = .22f, bloopVolume = .28f, rippleVolume = .5f, gulpVolume = .55f;
    [Range(0, 1)] public float popChance = .2f;               // share of RisingBubbles bubbles heard bursting
    public float referenceRate = 4.6f;                       // RisingBubbles emission the volumes are tuned for
    public float rippleGap = .4f;                            // shortest time between two ripple sounds

    const int Voices = 4;
    AudioSource waterSource, bubbleSource;
    readonly AudioSource[] voices = new AudioSource[Voices];
    int nextVoice;
    float stir, bubbleLevel, nextRipple;
    ParticleSystem.Particle[] particles;
    Dictionary<uint, Vector4> alive = new Dictionary<uint, Vector4>(), seen = new Dictionary<uint, Vector4>();

    void Awake()
    {
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

        if (bubbles != null) HearBursts(bubbles);
    }

    // A bubble missing since last frame has burst: some of them pop where they were last seen.
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
        float small = main.startSize.constantMin, big = main.startSize.constantMax;
        foreach (var bubble in seen)
        {
            if (alive.ContainsKey(bubble.Key) || Random.value > popChance) continue;
            float size = Mathf.InverseLerp(small, big, bubble.Value.w);
            Play(Pick(bloops), bubble.Value, bloopVolume * Mathf.Lerp(.45f, 1, size),
                Mathf.Lerp(1.15f, .85f, size) * Random.Range(.97f, 1.03f));
        }
    }

    // One bubble popping at the surface above `at`; also for bubbles the fish blows.
    public void PlayBloop(Vector3 at, float volume = 1) =>
        Play(Pick(bloops), at, bloopVolume * volume, Random.Range(.9f, 1.12f));

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
        if (Time.time < nextRipple) return;
        nextRipple = Time.time + rippleGap;
        Play(Pick(ripples), point, rippleVolume * Random.Range(.7f, 1), Random.Range(.9f, 1.1f));
    }

    void Play(AudioClip clip, Vector3 at, float volume, float pitch)
    {
        if (clip == null) return;
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
