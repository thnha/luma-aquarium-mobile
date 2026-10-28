using System;
using UnityEngine;

// How the fish feels (added to it at runtime by BettaFishAI):
// - Happiness: up with food, play (taps it swims to, greetings, new decor), down with hunger, boredom and
//   frights; drifts back toward a level set by how close it is to you.
// - Stress: a fright's after-effect, gone within half a minute.
// - Boredom: builds while nobody plays with it (not at night), wiped out by any attention.
// - Bond: how well it knows you. Grows slowly with feeding and play (at most dailyBondGain a day),
//   shrinks a little with frights and with days the app stays closed.
// Happiness and bond are saved per fish (its BettaVariant) between app sessions.
public sealed class BettaMood : MonoBehaviour
{
    public enum Feeling { Normal, Happy, Bored, Scared, Hungry, Sleepy }

    public float boredomTime = 600f;              // seconds without attention to get fully bored
    public float stressDecay = 0.03f;             // per second
    public float happinessDrift = 0.002f;         // per second toward its resting level
    public float dailyBondGain = 0.1f;
    public float bondLossPerDayAway = 0.01f;      // after the first two days
    public float maxBondLossAway = 0.2f;

    const string StoragePrefix = "Lumaquarium.FishMood.v1.";

    [Serializable] class Record { public float happiness = 0.5f, bond, gainedToday; public long lastSeenTicks; public string day; }

    BettaFishAI fish;
    BettaAppearance appearance;
    Record record = new Record();
    string fishId;
    float saveAt;

    public float Happiness => record.happiness;
    public float Bond => record.bond;
    public float Stress { get; private set; }
    public float Boredom { get; private set; }

    public Feeling Current => fish && fish.IsSleeping ? Feeling.Sleepy
        : Stress > 0.45f ? Feeling.Scared
        : fish && fish.Fatigue > 0.4f ? Feeling.Hungry
        : Boredom > 0.6f ? Feeling.Bored
        : Happiness > 0.62f ? Feeling.Happy : Feeling.Normal;

    void Awake()
    {
        fish = GetComponent<BettaFishAI>();
        appearance = GetComponent<BettaAppearance>();
    }

    void Update()
    {
        // The fish shown can be swapped (fish shop): each one keeps its own record.
        string id = FishId();
        if (id != fishId) { if (fishId != null) Save(); Load(id); }

        float dt = Time.deltaTime;
        Stress = Mathf.MoveTowards(Stress, 0f, stressDecay * dt);
        bool night = fish && fish.IsSleeping;
        if (!night) Boredom = Mathf.Clamp01(Boredom + dt / Mathf.Max(1f, boredomTime));
        float fatigue = fish ? fish.Fatigue : 0f;
        float rest = Mathf.Clamp01(0.5f + 0.2f * Bond - 0.4f * fatigue - 0.3f * Boredom - 0.3f * Stress);
        record.happiness = Mathf.MoveTowards(record.happiness, rest, happinessDrift * dt);

        if (Time.unscaledTime >= saveAt) { saveAt = Time.unscaledTime + 30f; Save(); }
    }

    // Events from BettaFishAI.
    public void Fed() { Cheer(0.06f, 0.5f); GainBond(0.012f); }
    public void Played() { Cheer(0.02f, 0.5f); GainBond(0.004f); }
    public void Greeted() { Cheer(0.05f, 0.3f); GainBond(0.006f); }
    public void SawSomethingNew() { Cheer(0.05f, 0.6f); }
    public void Startled() { Stress = Mathf.Clamp01(Stress + 0.35f); Cheer(-0.1f, 0.2f); record.bond = Mathf.Max(0f, record.bond - 0.02f); }
    public void Spooked() { Stress = Mathf.Clamp01(Stress + 0.15f); Cheer(-0.03f, 0f); }

    void Cheer(float happiness, float unbore)
    {
        record.happiness = Mathf.Clamp01(record.happiness + happiness);
        Boredom = Mathf.Max(0f, Boredom - unbore);
    }

    // Slower the closer it already is, and only so much a day.
    void GainBond(float amount)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (record.day != today) { record.day = today; record.gainedToday = 0f; }
        float gain = Mathf.Min(amount * (1f - record.bond), dailyBondGain - record.gainedToday);
        if (gain <= 0f) return;
        record.bond = Mathf.Clamp01(record.bond + gain);
        record.gainedToday += gain;
    }

    string FishId()
    {
        var variant = appearance ? appearance.Variant : null;
        return variant ? variant.name : gameObject.name;
    }

    void Load(string id)
    {
        fishId = id;
        record = new Record();
        string json = PlayerPrefs.GetString(StoragePrefix + id, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, record); }
            catch (ArgumentException) { record = new Record(); }
        }
        // Days away: it has missed you, and forgets you a little after the first couple of days.
        if (record.lastSeenTicks > 0)
        {
            double days = (DateTime.UtcNow - new DateTime(record.lastSeenTicks, DateTimeKind.Utc)).TotalDays;
            if (days > 2.0) record.bond = Mathf.Max(0f, record.bond - Mathf.Min(maxBondLossAway, (float)(days - 2.0) * bondLossPerDayAway));
            if (days > 0.5) record.happiness = Mathf.Min(record.happiness, 0.45f);
        }
        Stress = 0f;
        Boredom = 0f;
    }

    void Save()
    {
        if (Lumaquarium.Account.AccountSignIn.ProgressBlocked) return;
        if (fishId == null) return;
        record.lastSeenTicks = DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(StoragePrefix + fishId, JsonUtility.ToJson(record));
        PlayerPrefs.Save();
    }

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnDestroy() => Save();
}
