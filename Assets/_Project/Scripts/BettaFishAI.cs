using UnityEngine;

// Lightweight FSM from the GDD: Wander, Idle, SeekFood, Eat, Rest, plus Breathe
// (bettas are labyrinth fish and rise to gulp air at the surface every few minutes).
// The fish always swims along its own forward axis and turns gradually,
// so it never slides sideways or snaps orientation.
public class BettaFishAI : MonoBehaviour
{
    public enum State { Wander, Idle, SeekFood, Eat, Rest, Breathe }
    public State state = State.Wander;
    public BoxCollider swimBounds;
    public FeedingSystem feeding;
    public Animator animator;

    [Header("Movement")]
    public float swimSpeed = 0.3f;
    public float seekSpeedMultiplier = 1.6f;
    public float acceleration = 0.9f;
    public float deceleration = 0.22f;            // water drag when coasting: slow to lose speed
    public float turnDegreesPerSecond = 100f;     // top turn rate at full swim speed
    public float hoverTurnDegreesPerSecond = 50f; // turn rate when nearly stopped
    public float turnAcceleration = 220f;         // deg/s^2, eases turns in and out
    public float maxPitch = 15f;                  // while wandering; bettas mostly swim level
    public float seekMaxPitch = 35f;              // chasing food can climb/dive steeper
    public float pitchDegreesPerSecond = 25f;
    public float wanderHeightRange = 0.35f;       // max height change per wander leg
    public float arrivalRadius = 0.12f;
    public float slowdownRadius = 0.5f;
    public float boundsInset = 0.15f;

    [Header("Burst and coast")]
    public float burstMinTime = 0.7f;
    public float burstMaxTime = 1.5f;
    public float coastMinTime = 0.6f;
    public float coastMaxTime = 1.4f;
    public float coastSpeedFactor = 0.3f;         // target speed while gliding, relative to swimSpeed

    [Header("Surface breathing")]
    public float breatheMinInterval = 60f;
    public float breatheMaxInterval = 180f;
    public float breatheMaxPitch = 50f;
    public float gulpDuration = 0.5f;
    public float surfaceReach = 0.1f;             // above the SwimBounds top (the water surface is 0.1 above it)

    [Header("Resting")]
    [Range(0f, 1f)] public float bottomRestChance = 0.6f; // otherwise rests just under the surface
    public float restRoll = 12f;                  // bettas lie slightly tilted on the bottom
    public float bottomRestY = 0.43f;             // pivot height when lying on the substrate (sand top + belly)

    [Header("Feeding strike")]
    public float strikeRadius = 0.3f;             // close and lined up: dart at the pellet
    public float strikeSpeedMultiplier = 2.2f;

    [Header("Hovering")]
    public float hoverDrift = 0.012f;             // m/s of slow drift while hovering
    public float hoverYawDrift = 6f;              // deg/s of slow heading wander while hovering
    public float verticalHoverSpeed = 0.1f;       // m/s straight up/down on the pectorals when a target is too steep

    [Header("Timing")]
    public float wanderMinTime = 3f;
    public float wanderMaxTime = 7f;
    public float idleMinTime = 1.5f;
    public float idleMaxTime = 4f;
    public float restDuration = 6f;
    public float eatDuration = 0.6f;
    [Range(0f, 1f)] public float idleChance = 0.35f;
    [Range(0f, 1f)] public float restChance = 0.1f;

    [Header("Needs")]
    public float hunger = 0.5f; // 0 full; 1 hungry
    public float hungerPerSecond = 0.0004f;
    public float hungerPerPellet = 0.15f;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int NibbleId = Animator.StringToHash("Nibble");

    private Vector3 destination;
    private FoodPellet food;
    private float stateTimer;
    private float currentSpeed;
    private float currentTurnSpeed;
    private bool coasting;
    private float strokeTimer;
    private float targetEffort;
    private float yaw, pitch;                     // heading kept as angles so turns never roll
    private float breatheTimer;
    private bool gulping;
    private float ceiling;                        // top of the allowed space; lowers gently after a gulp
    private float floor;                          // bottom of the allowed space; drops to the sand to rest
    private float noiseSeed;
    private bool settled;                         // resting: reached the rest spot
    private bool restOnBottom;
    private float roll;

    // True only once the fish has reached its rest spot and lies still.
    public bool IsResting => state == State.Rest && settled;
    // 0 calm .. 1 excited (feeding, breathing, flaring); -1 resting. Drives dorsal/anal fin posture.
    public float Arousal => state == State.SeekFood || state == State.Eat || state == State.Breathe ? 1f
        : state == State.Rest ? (settled ? -1f : -0.3f) : state == State.Idle ? 0.2f : 0.35f;

    public float CurrentSpeed => currentSpeed;
    // 0 = hovering/gliding, 1 = actively beating the tail. Drives the caudal fin.
    public float Effort { get; private set; }
    // Set each frame by BettaSwimMotion so forward motion surges with each tail stroke.
    public float ThrustPulse { get; set; } = 1f;

    void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        breatheTimer = Random.Range(breatheMinInterval * 0.3f, breatheMaxInterval * 0.5f);
        noiseSeed = Random.Range(0f, 100f);
        ceiling = float.MaxValue;
        floor = float.MinValue;
        EnterWander();
    }

    void Update()
    {
        if (swimBounds == null) return;
        hunger = Mathf.Clamp01(hunger + hungerPerSecond * Time.deltaTime);
        stateTimer -= Time.deltaTime;

        if (state != State.SeekFood && state != State.Eat) LookForFood();
        breatheTimer -= Time.deltaTime;
        if (breatheTimer <= 0f && (state == State.Wander || state == State.Idle)) EnterBreathe();

        switch (state)
        {
            case State.Wander:
                UpdateBurstAndCoast();
                targetEffort = coasting ? 0.15f : 1f;
                SwimTowards(destination, coasting ? swimSpeed * coastSpeedFactor : swimSpeed);
                if (stateTimer <= 0f || Arrived(destination)) ChooseNextCalmState();
                break;
            case State.Rest:
                if (!settled)
                {
                    // Glide calmly to a rest spot on the bottom or just under the surface.
                    targetEffort = 0.4f;
                    SwimTowards(destination, swimSpeed * 0.5f);
                    if (Arrived(destination) || stateTimer <= 0f)
                    {
                        restOnBottom &= Arrived(destination); // gave up mid-water: hang level instead of lying tilted
                        settled = true;
                        stateTimer = restDuration;
                    }
                    break;
                }
                targetEffort = 0.05f;
                SwimTowards(transform.position + transform.forward, 0f);
                // Settle the last few centimetres down onto the rest spot.
                if (restOnBottom)
                    transform.position = Vector3.MoveTowards(transform.position, destination, 0.03f * Time.deltaTime);
                if (stateTimer <= 0f) EnterWander();
                break;
            case State.Idle:
                targetEffort = 0.1f;
                SwimTowards(transform.position + transform.forward, 0f);
                if (stateTimer <= 0f) EnterWander();
                break;
            case State.SeekFood:
                if (food == null) { LookForFood(); if (food == null) EnterWander(); break; }
                targetEffort = 1f;
                Vector3 target = ClampToBounds(food.transform.position, SurfaceTop());
                Vector3 toFood = target - transform.position;
                bool strike = toFood.magnitude < strikeRadius && Vector3.Dot(transform.forward, toFood.normalized) > 0.85f;
                SwimTowards(target, swimSpeed * (strike ? strikeSpeedMultiplier : seekSpeedMultiplier), strike);
                if (Arrived(target)) EnterEat();
                break;
            case State.Breathe:
                if (!gulping)
                {
                    // Keep the target just ahead and above, so the fish climbs in a steep
                    // forward line instead of overshooting and circling under it.
                    // Up against the glass, swing back toward the middle of the tank first.
                    Vector3 here = transform.position;
                    Vector3 ahead = ClampToBounds(here + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 0.15f);
                    if (new Vector2(ahead.x - here.x, ahead.z - here.z).sqrMagnitude < 0.01f)
                        ahead = ClampToBounds(here + Vector3.ProjectOnPlane(swimBounds.bounds.center - here, Vector3.up).normalized * 0.15f);
                    destination.x = ahead.x;
                    destination.z = ahead.z;
                    targetEffort = 1f;
                    SwimTowards(destination, swimSpeed);
                    if (Arrived(destination) || transform.position.y >= destination.y - 0.02f)
                    {
                        gulping = true;
                        stateTimer = gulpDuration;
                        if (animator) animator.SetTrigger(NibbleId);
                    }
                }
                else
                {
                    // Hold the snout tipped up into the surface while gulping.
                    targetEffort = 0.2f;
                    SwimTowards(transform.position + Vector3.up * 0.1f + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 0.2f, 0f);
                    if (stateTimer <= 0f) EnterDive();
                }
                break;
            case State.Eat:
                targetEffort = 0.2f;
                SwimTowards(transform.position + transform.forward, 0f);
                if (stateTimer <= 0f)
                {
                    LookForFood();
                    if (food == null) EnterIdle();
                }
                break;
        }

        Effort = Mathf.MoveTowards(Effort, targetEffort, 3f * Time.deltaTime);
        // While breathing the fish may reach the surface; afterwards the ceiling follows it
        // back down instead of snapping it under the normal limit.
        float normalTop = NormalTop();
        bool surfaceAllowed = state == State.Breathe || state == State.SeekFood || state == State.Eat;
        ceiling = surfaceAllowed ? SurfaceTop() : Mathf.Max(normalTop, Mathf.Min(ceiling, transform.position.y));
        float normalBottom = NormalBottom();
        floor = state == State.Rest && restOnBottom ? Mathf.Min(normalBottom, bottomRestY) : Mathf.Min(normalBottom, Mathf.Max(floor, transform.position.y));
        transform.position = ClampToBounds(transform.position, ceiling, floor);
        if (animator) animator.SetFloat(SpeedId, currentSpeed);
    }

    // Bettas swim in short tail-driven bursts, then glide while drag slows them down.
    void UpdateBurstAndCoast()
    {
        strokeTimer -= Time.deltaTime;
        if (strokeTimer > 0f) return;
        coasting = !coasting;
        strokeTimer = coasting ? Random.Range(coastMinTime, coastMaxTime) : Random.Range(burstMinTime, burstMaxTime);
    }

    public void Seek(FoodPellet pellet)
    {
        if (pellet == null || state == State.Eat) return;
        food = pellet;
        state = State.SeekFood;
    }

    void LookForFood()
    {
        FoodPellet nearest = feeding ? feeding.NearestPellet(transform.position) : null;
        if (nearest != null) Seek(nearest);
    }

    void SwimTowards(Vector3 target, float targetSpeed, bool strike = false)
    {
        Vector3 direction = target - transform.position;
        // Heading the fish can actually swim: yaw toward the target, pitch within the
        // limit for this state. Yaw and pitch are steered separately and rebuilt with zero
        // roll; slerping the whole rotation takes a shortcut that banks the fish mid-turn.
        float targetYaw = yaw, targetPitch = 0f;
        bool tooSteep = false;
        if (direction.sqrMagnitude > 0.0001f)
        {
            targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float limit = state == State.SeekFood || state == State.Rest ? seekMaxPitch
                : state == State.Breathe ? breatheMaxPitch : maxPitch;
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            if (targetSpeed > 0f || state == State.Breathe)
            {
                float rawPitch = -Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
                targetPitch = Mathf.Clamp(rawPitch, -limit, limit);
                tooSteep = Mathf.Abs(rawPitch) > limit && horizontal < 0.2f;
            }
        }

        // Pectoral braking: near the target, turning hard or stopping, shed speed quickly.
        // Only free gliding keeps the slow, drag-limited deceleration.
        bool braking = targetSpeed <= 0f;
        if (targetSpeed > 0f && !strike)
        {
            // Slow down near the target and while turning, so the turn radius
            // stays smaller than arrivalRadius and the fish can't orbit forever.
            // Alignment is measured against the reachable heading, so a target straight
            // above (steeper than the pitch limit) doesn't stall the fish.
            Vector3 reachable = Quaternion.Euler(targetPitch, targetYaw, 0f) * Vector3.forward;
            float alignment = Vector3.Dot(transform.forward, reachable) * 0.5f + 0.5f;
            float approach = Mathf.Clamp01(direction.magnitude / slowdownRadius);
            targetSpeed *= Mathf.Max(0.15f, alignment * alignment) * Mathf.Max(0.2f, approach);
            braking = approach < 1f || alignment < 0.6f;
        }
        // A strike is a short dart; the fish then brakes hard with its pectorals.
        float rate = targetSpeed > currentSpeed ? acceleration * (strike ? 4f : 1f)
            : braking ? acceleration * (currentSpeed > swimSpeed * 1.2f ? 3f : 1f) : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);

        // Turn rate grows with swim speed and eases off as the heading lines up,
        // so turns start and finish smoothly instead of snapping at a fixed rate.
        float desiredTurnSpeed = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            float maxRate = Mathf.Lerp(hoverTurnDegreesPerSecond, turnDegreesPerSecond, Mathf.Clamp01(currentSpeed / swimSpeed));
            desiredTurnSpeed = Mathf.Min(maxRate, Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) * 2.5f);
        }
        currentTurnSpeed = Mathf.MoveTowards(currentTurnSpeed, desiredTurnSpeed, turnAcceleration * Time.deltaTime);
        yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, currentTurnSpeed * Time.deltaTime);
        // Level out while hovering, and don't pitch steeply in the middle of a sharp turn.
        float turnFactor = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) / 90f);
        pitch = Mathf.MoveTowards(pitch, targetPitch * turnFactor, pitchDegreesPerSecond * Time.deltaTime);
        // Lying on the bottom the fish settles onto one side a little.
        float targetRoll = IsResting && restOnBottom ? restRoll : 0f;
        roll = Mathf.MoveTowards(roll, targetRoll, 8f * Time.deltaTime);
        transform.rotation = Quaternion.Euler(pitch, yaw, roll);
        transform.position += transform.forward * currentSpeed * ThrustPulse * Time.deltaTime;
        // Target almost straight above/below: rise or sink on the pectorals instead of circling.
        if (tooSteep)
            transform.position += Vector3.up * Mathf.Clamp(direction.y, -verticalHoverSpeed * Time.deltaTime, verticalHoverSpeed * Time.deltaTime);

        // A hovering betta is never pinned in place: it drifts and bobs a few millimetres
        // and lets its heading wander while the pectorals keep correcting.
        float hover = 1f - Mathf.Clamp01(currentSpeed / 0.05f);
        if (hover > 0f && targetSpeed <= 0f)
        {
            float t = Time.time * 0.25f;
            Vector3 drift = new Vector3(Mathf.PerlinNoise(t, noiseSeed) - 0.5f,
                Mathf.PerlinNoise(noiseSeed, t) - 0.5f, Mathf.PerlinNoise(t + 31f, noiseSeed + 17f) - 0.5f) * 2f;
            float still = IsResting ? 0.3f : 1f;
            transform.position += drift * hoverDrift * hover * still * Time.deltaTime;
            yaw += still * (Mathf.PerlinNoise(t * 0.7f, noiseSeed + 53f) - 0.5f) * 2f * hoverYawDrift * hover * Time.deltaTime;
        }
    }

    bool Arrived(Vector3 target) => (target - transform.position).sqrMagnitude <= arrivalRadius * arrivalRadius;

    void ChooseNextCalmState()
    {
        float dice = Random.value;
        if (dice < restChance) EnterRest();
        else if (dice < restChance + idleChance) EnterIdle();
        else EnterWander();
    }

    void EnterWander()
    {
        state = State.Wander;
        Bounds b = swimBounds ? swimBounds.bounds : new Bounds(transform.position, Vector3.one);
        float y = transform.position.y + Random.Range(-wanderHeightRange, wanderHeightRange);
        destination = ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, Random.Range(b.min.z, b.max.z)));
        stateTimer = Random.Range(wanderMinTime, wanderMaxTime);
    }

    void EnterBreathe()
    {
        state = State.Breathe;
        gulping = false;
        breatheTimer = Random.Range(breatheMinInterval, breatheMaxInterval);
        Vector3 ahead = transform.position + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 0.2f;
        destination = ClampToBounds(ahead);
        destination.y = SurfaceTop();
    }

    // After a gulp the fish turns and glides back down.
    void EnterDive()
    {
        state = State.Wander;
        Bounds b = swimBounds ? swimBounds.bounds : new Bounds(transform.position, Vector3.one);
        float y = transform.position.y - Random.Range(0.3f, 0.6f);
        destination = ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, Random.Range(b.min.z, b.max.z)));
        stateTimer = Random.Range(wanderMinTime, wanderMaxTime);
    }

    float SurfaceTop() => swimBounds ? swimBounds.bounds.max.y + surfaceReach : float.MaxValue;

    float NormalBottom() => swimBounds ? swimBounds.bounds.min.y + boundsInset : float.MinValue;

    float NormalTop() => swimBounds ? swimBounds.bounds.max.y - boundsInset : float.MaxValue;

    // Bettas rest on the bottom (or on leaves) or hang just under the surface.
    void EnterRest()
    {
        state = State.Rest;
        settled = false;
        restOnBottom = Random.value < bottomRestChance;
        Bounds b = swimBounds ? swimBounds.bounds : new Bounds(transform.position, Vector3.one);
        float y = restOnBottom ? Mathf.Min(NormalBottom(), bottomRestY) : NormalTop();
        // On the bottom keep to the open front half; rocks and plants fill the back.
        float zMax = restOnBottom ? Mathf.Min(b.max.z, b.center.z - 0.05f) : b.max.z;
        destination = ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, Random.Range(b.min.z, zMax)), NormalTop(), y);
        // Give up travelling after this long and rest where it is.
        stateTimer = 6f + Vector3.Distance(transform.position, destination) / 0.08f;
    }

    void EnterIdle()
    {
        state = State.Idle;
        stateTimer = Random.Range(idleMinTime, idleMaxTime);
    }

    void EnterEat()
    {
        // Snap the pellet on arrival; the Eat state is the short pause to swallow.
        state = State.Eat;
        stateTimer = eatDuration;
        if (animator) animator.SetTrigger(NibbleId);
        if (food != null)
        {
            food.Consume();
            hunger = Mathf.Clamp01(hunger - hungerPerPellet);
        }
        food = null;
    }

    Vector3 ClampToBounds(Vector3 position) => ClampToBounds(position, NormalTop(), NormalBottom());

    Vector3 ClampToBounds(Vector3 position, float top) => ClampToBounds(position, top, NormalBottom());

    Vector3 ClampToBounds(Vector3 position, float top, float bottom)
    {
        if (swimBounds == null) return position;
        Bounds b = swimBounds.bounds;
        return new Vector3(Mathf.Clamp(position.x, b.min.x + boundsInset, b.max.x - boundsInset),
            Mathf.Clamp(position.y, bottom, top),
            Mathf.Clamp(position.z, b.min.z + boundsInset, b.max.z - boundsInset));
    }
}
