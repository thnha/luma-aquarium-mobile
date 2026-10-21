using System.Collections.Generic;
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
    // The pivot (gills) is the head: it swings toward the new heading first and the body
    // follows its path (BettaSwimMotion), so the head turns, then the body, then the tail.
    // A swimming fish changes direction along an arc instead of spinning on the spot:
    // the turn rate follows forward speed (speed / radius), with a slow pectoral turn when hovering.
    // The radius is in body lengths so a big fish turns wider; a tighter arc than about half a
    // body length would need the spine to coil further than the rig can bend.
    public float turnRadiusBodyLengths = 0.5f;    // larger = wider, calmer turns
    public float headTurnRate = 140f;             // deg/s cap
    public float hoverHeadTurnRate = 40f;         // deg/s turning on the spot while holding position
    public float turnGlideSpeed = 0.1f;           // m/s it always glides forward while the heading is well off
    [Range(0.1f, 1f)] public float closeTurnRadiusFactor = 0.4f; // tightest arc near the target, share of the normal radius
    public float headTurnGain = 2f;               // deg/s of turn rate per degree still to go: eases out
    public float headTurnAcceleration = 300f;     // deg/s^2, how gradually the swing starts and stops
    [Range(0f, 1f)] public float minTurnSpeedFactor = 0.8f; // keeps swimming through a U-turn instead of stopping
    public float turnExitBoost = 1.3f;            // speed multiplier as the tail sweeps out of a big turn
    public float turnExitBoostTime = 0.5f;
    public float maxPitch = 15f;                  // while wandering; bettas mostly swim level
    public float feedMaxPitch = 60f;              // darting up to floating food is steep
    public float restMaxPitch = 35f;              // gliding down to a rest spot
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
    public float waterSurfaceY = 2.35f;           // world height of the water surface
    public float gulpDepth = 0.04f;               // pivot depth below the surface when gulping or taking floating food

    [Header("Resting")]
    [Range(0f, 1f)] public float bottomRestChance = 0.6f; // otherwise rests just under the surface
    public float restRoll = 12f;                  // bettas lie slightly tilted on the bottom
    public float bottomRestY = 0.62f;             // pivot height resting on the bottom: long anal/pelvic fins just touch the sand

    [Header("Feeding strike")]
    public float strikeRadius = 0.3f;             // close and lined up: dart at the pellet
    public float strikeSpeedMultiplier = 2.2f;
    public float mouthOffset = 0.04f;             // mouth distance ahead of the pivot

    [Header("Hovering")]
    public float hoverDrift = 0.012f;             // m/s of slow drift while hovering
    public float hoverYawDrift = 6f;              // deg/s of slow heading wander while hovering
    public float verticalHoverSpeed = 0.1f;       // m/s straight up/down on the pectorals when a target is too steep

    [Header("Obstacles")]
    // Rocks, driftwood and coral are solid (their placement BoxCollider); plants are not.
    // The fish looks ahead and steers around or over them, wander/rest spots are lifted
    // above them, and any remaining overlap is pushed out.
    public float obstacleLookAhead = 0.3f;        // metres ahead of the snout
    public float bodyRadiusBodyLengths = 0.12f;   // half the body depth, in body lengths
    public float finHangBodyLengths = 0.4f;       // long anal/pelvic fins hang this far below the body
    public float obstacleClearance = 0.04f;       // extra room it keeps while steering
    public float avoidStrength = 2.5f;
    public float maxClimb = 0.3f;                 // lower obstacles are swum over, taller ones around
    public float climbLookAhead = 0.8f;           // metres ahead it starts rising to clear something
    public float climbMaxPitch = 40f;             // may tilt this steeply while rising over decor

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
    private float yawVelocity;                    // signed deg/s, so a turn eases in and out
    private bool bigTurn;
    private float turnExitTimer;
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
    private readonly List<Collider> obstacles = new List<Collider>();
    private AquariumDecoration decoration;
    private float obstacleScanTimer;
    private bool climbingObstacle;

    // True only once the fish has reached its rest spot and lies still.
    public bool IsResting => state == State.Rest && settled;
    // 0 calm .. 1 excited (feeding, breathing, flaring); -1 resting. Drives dorsal/anal fin posture.
    public float Arousal => state == State.SeekFood || state == State.Eat || state == State.Breathe ? 1f
        : state == State.Rest ? (settled ? -1f : -0.3f) : state == State.Idle ? 0.2f : 0.35f;

    public float CurrentSpeed => currentSpeed;
    // Gills to tail base in metres; set by BettaSwimMotion from the model's rig.
    public float BodyLength { get; set; } = 0.3f;
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
                bool strike = toFood.magnitude < strikeRadius && Vector3.Dot(transform.forward, toFood.normalized) > 0.7f;
                SwimTowards(target, swimSpeed * (strike ? strikeSpeedMultiplier : seekSpeedMultiplier), strike);
                // Measured from the mouth, a little ahead of the pivot (which sits at the gills).
                Vector3 mouth = transform.position + transform.forward * mouthOffset;
                if (Arrived(target) || (food.transform.position - mouth).sqrMagnitude <= arrivalRadius * arrivalRadius) EnterEat();
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
        ResolveObstacles();
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
        if (targetSpeed > 0f) direction = AvoidObstacles(direction);
        // Heading the fish can actually swim: yaw toward the target, pitch within the
        // limit for this state. Yaw and pitch are steered separately and rebuilt with zero
        // roll; slerping the whole rotation takes a shortcut that banks the fish mid-turn.
        float targetYaw = yaw, targetPitch = 0f;
        bool tooSteep = false;
        float horizontal = new Vector2(direction.x, direction.z).magnitude;
        if (direction.sqrMagnitude > 0.0001f)
        {
            // Straight above/below the heading is meaningless; keep the current one.
            if (horizontal > 0.03f) targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float limit = state == State.SeekFood ? feedMaxPitch : state == State.Rest ? restMaxPitch
                : state == State.Breathe ? breatheMaxPitch : maxPitch;
            if (climbingObstacle) limit = Mathf.Max(limit, climbMaxPitch);
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
            targetSpeed *= Mathf.Max(minTurnSpeedFactor, alignment * alignment) * Mathf.Max(0.2f, approach);
            braking = approach < 1f || alignment < 0.6f;
            // Coming out of a big turn the tail sweeps through and pushes the fish off.
            if (turnExitTimer > 0f)
            {
                targetSpeed *= turnExitBoost;
                braking = false;
            }
            // A betta never stands still and spins toward a new heading: while the heading
            // is still well off it glides forward a little and turns along that glide.
            float offHeading = Mathf.Clamp01((Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) - 15f) / 30f);
            targetSpeed = Mathf.Max(targetSpeed, turnGlideSpeed * offHeading);
        }
        // A strike is a short dart; the fish then brakes hard with its pectorals.
        float rate = targetSpeed > currentSpeed ? acceleration * (strike ? 4f : 1f)
            : braking ? acceleration * (currentSpeed > swimSpeed * 1.2f ? 3f : 1f) : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);

        // The head swings quickly toward the new heading and eases off as it lines up.
        // The rate is signed and acceleration-limited, so the swing starts and stops smoothly
        // and never jumps when the target changes side.
        float yawError = Mathf.DeltaAngle(yaw, targetYaw);
        float desiredYawVelocity = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            // Travelling to a target the fish turns only as fast as its forward glide allows
            // (speed / radius). Close to the target the arc tightens on the pectorals, so a
            // target just behind is reached instead of circled. Only a fish holding position
            // (target speed 0) turns on the spot, slowly.
            float turnRadius = Mathf.Max(0.01f, turnRadiusBodyLengths * BodyLength);
            turnRadius = Mathf.Max(turnRadius * closeTurnRadiusFactor, Mathf.Min(turnRadius, horizontal * 0.5f));
            float maxRate = targetSpeed > 0f ? Mathf.Min(currentSpeed / turnRadius * Mathf.Rad2Deg, headTurnRate) : hoverHeadTurnRate;
            desiredYawVelocity = Mathf.Clamp(yawError * headTurnGain, -maxRate, maxRate);
        }
        yawVelocity = Mathf.MoveTowards(yawVelocity, desiredYawVelocity, headTurnAcceleration * Time.deltaTime);
        yaw += yawVelocity * Time.deltaTime;
        if (Mathf.Abs(yawError) > 100f) bigTurn = true;
        else if (bigTurn && Mathf.Abs(yawError) < 25f)
        {
            bigTurn = false;
            turnExitTimer = turnExitBoostTime;
        }
        turnExitTimer -= Time.deltaTime;
        // Level out while hovering, and don't pitch steeply in the middle of a sharp turn.
        // (Unless the target is nearly overhead/below, where the yaw error barely matters.)
        float turnFactor = horizontal < 0.15f ? 1f : Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) / 90f);
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
        destination = AboveObstacles(ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, Random.Range(b.min.z, b.max.z))));
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
        destination = AboveObstacles(ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, Random.Range(b.min.z, b.max.z))));
        stateTimer = Random.Range(wanderMinTime, wanderMaxTime);
    }

    float SurfaceTop() => waterSurfaceY - gulpDepth;

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
        // Over a rock or driftwood it rests on top of it, as bettas like to.
        destination = AboveObstacles(ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, Random.Range(b.min.z, zMax)), NormalTop(), y));
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

    float BodyRadius => bodyRadiusBodyLengths * BodyLength;
    float FinHang => finHangBodyLengths * BodyLength;

    void ScanObstacles()
    {
        obstacleScanTimer -= Time.deltaTime;
        // A decoration removed between scans leaves a destroyed collider in the list.
        obstacles.RemoveAll(col => !col);
        if (obstacleScanTimer > 0f) return;
        obstacleScanTimer = 0.5f;                 // decorations can be added and moved at any time
        obstacles.Clear();
        if (!decoration) decoration = FindFirstObjectByType<AquariumDecoration>();
        foreach (var item in FindObjectsByType<AquariumDecorItem>(FindObjectsSortMode.None))
        {
            if (item.floats || item.wallMounted) continue;
            var offer = decoration && decoration.catalog ? decoration.catalog.Find(item.kind) : null;
            bool solid = offer != null ? offer.category != AquariumDecorCategory.Plants : item.foliage == null;
            var col = item.GetComponent<Collider>();
            if (solid && col && col.enabled) obstacles.Add(col);
        }
    }

    // Points along the body, snout to tail base; the rear ones also check where the fins hang.
    static readonly float[] BodySamples = { 0.15f, 0f, -0.35f, -0.7f, -1f };

    // Height the pivot needs for the body and hanging fins to clear this obstacle.
    float ClearHeight(Collider col) => col.bounds.max.y + FinHang + BodyRadius;

    // Swim over it when it is low, or when the tank is too shallow front to back to pass
    // beside it (and it isn't taller than the swimming space); otherwise go around.
    bool ClimbOver(Collider col)
    {
        float clear = ClearHeight(col);
        if (clear > NormalTop()) return false;
        if (clear - transform.position.y < maxClimb) return true;
        Bounds b = col.bounds, swim = swimBounds.bounds;
        float r = BodyRadius;
        bool roomInFront = b.min.z - r > swim.min.z + boundsInset;
        bool roomBehind = b.max.z + r < swim.max.z - boundsInset;
        return !roomInFront && !roomBehind;
    }

    // Steers the swim direction around or over decorations ahead.
    Vector3 AvoidObstacles(Vector3 direction)
    {
        ScanObstacles();
        climbingObstacle = false;
        if (obstacles.Count == 0) return direction;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 snout = transform.position + transform.forward * (BodySamples[0] * BodyLength);
        float r = BodyRadius, reach = r + obstacleClearance;

        // Over: rise early enough to clear what lies ahead, and hold the height until the
        // tail and its fins have passed (the stretch behind the snout is checked too).
        float slope = float.NegativeInfinity;
        foreach (var col in obstacles)
        {
            if (!ClimbOver(col)) continue;
            Bounds b = col.bounds;
            float needed = ClearHeight(col) - transform.position.y;
            for (float d = -BodyLength * 1.15f; d <= climbLookAhead; d += 0.05f)
            {
                Vector3 p = snout + forward * d;
                if (p.x < b.min.x - r || p.x > b.max.x + r || p.z < b.min.z - r || p.z > b.max.z + r) continue;
                slope = Mathf.Max(slope, needed / Mathf.Max(0.15f, d));
                break;
            }
        }

        // Around: push sideways away from what's just ahead.
        Vector3 steer = Vector3.zero;
        for (int k = 1; k <= 3; k++)
        {
            Vector3 ahead = snout + forward * (obstacleLookAhead * k / 3f);
            foreach (var col in obstacles)
            {
                if (ClimbOver(col)) continue;
                for (int f = 0; f < 2; f++)
                {
                    Vector3 point = f == 0 ? ahead : ahead + Vector3.down * FinHang;
                    Vector3 away = point - col.ClosestPoint(point);
                    float distance = away.magnitude;
                    if (distance >= reach) continue;
                    // Head-on, push straight sideways, away from the obstacle's centre.
                    Vector3 lateral = Vector3.ProjectOnPlane(away, forward);
                    lateral.y = 0f;
                    if (lateral.sqrMagnitude < 1e-6f)
                        lateral = side * (Vector3.Dot(side, point - col.bounds.center) >= 0f ? 1f : -1f);
                    steer += lateral.normalized * (1f - distance / reach) / k;
                }
            }
        }

        bool climb = slope > float.NegativeInfinity;
        if (steer == Vector3.zero && !climb) return direction;
        Vector3 result = direction.normalized + steer * avoidStrength;
        if (climb)
        {
            // Never descend into it; rise along the slope that clears it in time.
            Vector3 flat = new Vector3(result.x, 0f, result.z);
            float run = Mathf.Max(0.05f, flat.magnitude);
            result.y = Mathf.Max(result.y, slope * run);
            climbingObstacle = slope > 0f;
        }
        return result * direction.magnitude;
    }

    // Last resort: push the body back out of anything it still overlaps.
    void ResolveObstacles()
    {
        ScanObstacles();
        if (obstacles.Count == 0) return;
        Vector3 push = Vector3.zero;
        for (int i = 0; i < BodySamples.Length; i++)
        {
            Vector3 body = transform.position + transform.forward * (BodySamples[i] * BodyLength);
            for (int f = 0; f < (i == 0 ? 1 : 2); f++)
            {
                Vector3 point = f == 0 ? body : body + Vector3.down * FinHang;
                foreach (var col in obstacles)
                {
                    Vector3 closest = col.ClosestPoint(point);
                    Vector3 away = point - closest;
                    float distance = away.magnitude;
                    float radius = i == 0 ? BodyRadius * 0.6f : BodyRadius;
                    if (distance >= radius) continue;
                    if (distance < 1e-5f)
                    {
                        // Inside the box: leave through the top if that's closest, else sideways.
                        Bounds b = col.bounds;
                        away = b.max.y - point.y < Mathf.Min(b.extents.x, b.extents.z) ? Vector3.up
                            : Vector3.ProjectOnPlane(point - b.center, Vector3.up).normalized;
                        distance = 0f;
                    }
                    else away /= distance;
                    Vector3 needed = away * (radius - distance);
                    push = new Vector3(
                        Mathf.Abs(needed.x) > Mathf.Abs(push.x) ? needed.x : push.x,
                        Mathf.Abs(needed.y) > Mathf.Abs(push.y) ? needed.y : push.y,
                        Mathf.Abs(needed.z) > Mathf.Abs(push.z) ? needed.z : push.z);
                }
            }
        }
        if (push == Vector3.zero) return;
        transform.position = ClampToBounds(transform.position + push * Mathf.Clamp01(10f * Time.deltaTime), SurfaceTop(), float.MinValue);
    }

    // Lifts a spot so the fish (and its hanging fins) clears any decoration under it.
    Vector3 AboveObstacles(Vector3 position)
    {
        ScanObstacles();
        float r = BodyRadius;
        foreach (var col in obstacles)
        {
            Bounds b = col.bounds;
            if (position.x < b.min.x - r || position.x > b.max.x + r || position.z < b.min.z - r || position.z > b.max.z + r) continue;
            position.y = Mathf.Max(position.y, Mathf.Min(b.max.y + FinHang + r, NormalTop()));
        }
        return position;
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
