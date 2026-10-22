using System.Collections.Generic;
using UnityEngine;

// Lightweight FSM from the GDD: Wander, Idle, SeekFood, Eat, Rest, plus Breathe
// (bettas are labyrinth fish and rise to gulp air at the surface every few minutes).
// The fish always swims along its own forward axis and turns gradually,
// so it never slides sideways or snaps orientation.
public class BettaFishAI : MonoBehaviour
{
    public enum State { Wander, Idle, SeekFood, Eat, Rest, Breathe, SwimToTap, Flee }
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
    public float tapMaxPitch = 55f;               // heading up to a tap by the surface or down to the sand, or bolting
    public float steepClimbAssist = 0.3f;         // m/s it may rise/sink on the pectorals for a target steeper than that
    public float pitchDegreesPerSecond = 25f;
    public float wanderHeightRange = 0.35f;       // max height change per wander leg
    public float arrivalRadius = 0.12f;
    public float slowdownRadius = 0.5f;
    public float boundsInset = 0.15f;
    // The tank walls stop the whole fish, not just its gills: the tail and caudal fin reach this far
    // behind the pivot and the fins sway this far to the side (both in body lengths).
    public float tailReachBodyLengths = 1.3f;
    public float wallClearanceBodyLengths = 0.12f;
    // It turns away from the walls before reaching them instead of being pushed back by them.
    public float wallLookAhead = 0.35f;           // metres ahead of the snout it checks for glass/stone
    public float wallAvoidDistance = 0.15f;       // starts steering once a look-ahead point is this close
    // Overlap with walls and decor is pushed out over a few frames, like backing off on the
    // pectorals, never deeper than maxWallPenetration into the walls.
    public float contactPushRate = 8f;
    public float maxWallPenetration = 0.03f;

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
    [Range(0f, 1f)] public float bubbleBlowChance = 0.5f; // after a gulp, sometimes blows a few bubbles before diving
    public float bubbleBlowDuration = 1.1f;

    [Header("Resting")]
    [Range(0f, 1f)] public float bottomRestChance = 0.6f; // otherwise rests just under the surface
    public float restRoll = 12f;                  // bettas lie slightly tilted on the bottom
    public float bottomRestY = 0.62f;             // pivot height resting on the bottom: long anal/pelvic fins just touch the sand

    [Header("Feeding strike")]
    public float strikeRadius = 0.3f;             // close and lined up: dart at the pellet
    public float strikeSpeedMultiplier = 2.2f;
    public float mouthOffset = 0.04f;             // mouth distance ahead of the pivot
    public float bottomFeedY = 0.55f;             // lowest pivot height nosing a pellet off the sand
    public float seekGiveUpTime = 6f;             // gives up on a pellet it can't get closer to (wedged by decor)
    // Wandering, resting or heading for a tap it gives up on a spot it can't get to: barely moving
    // for stuckStillTime (wedged), or no closer for stuckNoProgressTime (sliding along decor).
    public float stuckMoveDistance = 0.06f;
    public float stuckStillTime = 1.5f;
    public float stuckNoProgressTime = 3f;
    public float tapSwimSpeedMultiplier = 2f;     // swimming over to where the player tapped
    public float tapAccelerationMultiplier = 2.5f; // darts off right after the tap
    public float tapExcitedSpeedMultiplier = 2.8f; // after several quick taps, or darting at a tap just ahead
    [Range(-1f, 1f)] public float tapAheadCone = 0.5f; // cos of the half-angle that counts as "in front" (60 deg)
    [Range(0f, 1f)] public float tapReflex = 0.6f; // quickness for any tap, held until it gets there
    public float tapExcitementPerTap = 0.35f;     // each tap excites it more; it calms at tapCalmRate per second
    public float tapCalmRate = 0.4f;
    public float quickTurnRate = 220f;            // deg/s whipping round on the pectorals when excited

    [Header("Startle")]
    public float fleeSpeedMultiplier = 3.2f;      // a tap right on the fish: it bolts away
    public float fleeDistance = 0.7f;             // how far it runs from the tap
    public float fleeDive = 0.25f;                // and drops a little deeper
    public float startleShyTime = 2.5f;           // ignores "come here" taps for a while afterwards

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
    public float obstacleClearance = 0.02f;       // extra room it keeps while steering
    public float avoidStrength = 2.5f;
    public float maxClimb = 0.3f;                 // lower obstacles are swum over, taller ones around
    public float climbLookAhead = 0.45f;          // metres ahead it starts rising to clear something
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
    static readonly int GulpId = Animator.StringToHash("Gulp");
    static readonly int BubbleId = Animator.StringToHash("Bubble");
    const float GulpClipSeconds = 20f / 24f;      // Gulp clip length (mouth_clips.py); a bubble blow waits for it

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
    private bool blowingBubbles;
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
    private float closestFoodDistance;
    private float seekStuckTimer;
    private Vector3 stuckDestination, stuckAnchor;
    private float closestDestinationDistance, stillTimer, noProgressTimer;
    private float wallContact;                    // how far the body was pushed off the walls last frame
    private float tapExcitement;                  // 0..1, built up by repeated taps
    private float shyUntil;
    private bool tapWasInFront;                   // the tap being swum to landed ahead of the fish
    private Collider detourAround;                // decor it is going round, and which way round
    private float detourSide;
    private bool bolting;                         // fleeing: turned away and running flat out

    // True only once the fish has reached its rest spot and lies still.
    public bool IsResting => state == State.Rest && settled;
    // 0 calm .. 1 excited (feeding, breathing, flaring); -1 resting. Drives dorsal/anal fin posture.
    public float Arousal => state == State.SeekFood || state == State.Eat || state == State.Breathe ? 1f
        : state == State.SwimToTap ? Mathf.Lerp(0.5f, 1f, tapExcitement) : state == State.Flee ? 1f
        : state == State.Rest ? (settled ? -1f : -0.3f) : state == State.Idle ? 0.2f : 0.35f;

    public float CurrentSpeed => currentSpeed;
    // Fired when the Gulp / Bubble clips start, so BettaMouth can let bubbles out in time with them.
    public event System.Action Gulped, BlowingBubbles;
    // Gills to tail base in metres; set by BettaSwimMotion from the model's rig.
    public float BodyLength { get; set; } = 0.3f;
    // 0 = hovering/gliding, 1 = actively beating the tail. Drives the caudal fin.
    public float Effort { get; private set; }
    // Set each frame by BettaSwimMotion so forward motion surges with each tail stroke.
    public float ThrustPulse { get; set; } = 1f;
    // The body as drawn, set each frame by BettaSwimMotion: offsets from the pivot to the spine
    // joints as they bend along the path (without the tail stroke), then to the caudal fin tip.
    public Vector3[] BodyLine { get; set; }

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
        tapExcitement = Mathf.MoveTowards(tapExcitement, 0f, tapCalmRate * Time.deltaTime);
        stateTimer -= Time.deltaTime;

        if (state != State.SeekFood && state != State.Eat && state != State.Flee) LookForFood();
        breatheTimer -= Time.deltaTime;
        // A frightened fish stays down rather than heading up for air straight away.
        if (breatheTimer <= 0f && (state == State.Wander || state == State.Idle) && Time.time >= shyUntil) EnterBreathe();

        switch (state)
        {
            case State.Wander:
                UpdateBurstAndCoast();
                targetEffort = coasting ? 0.15f : 1f;
                SwimTowards(destination, coasting ? swimSpeed * coastSpeedFactor : swimSpeed);
                if (stateTimer <= 0f || Arrived(destination)) ChooseNextCalmState();
                // Wedged in a gap or against the glass: head off somewhere open instead.
                else if (Stuck()) EnterWander(true);
                break;
            case State.Rest:
                if (!settled)
                {
                    // Glide calmly to a rest spot on the bottom or just under the surface.
                    targetEffort = 0.4f;
                    SwimTowards(destination, swimSpeed * 0.5f);
                    if (Arrived(destination) || stateTimer <= 0f || Stuck())
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
            case State.SwimToTap:
                targetEffort = 1f;
                // A tap right in front gets an instant dart, like a strike at food; repeated
                // taps excite it so it whips round and rushes over wherever they are.
                // Whether the tap was in front is decided when it lands: judged every frame, the
                // heading wobbling across the cone's edge would switch the dart on and off.
                Vector3 toTap = destination - transform.position;
                float quickness = Mathf.Max(tapExcitement, tapWasInFront ? Mathf.Max(0.8f, tapReflex) : tapReflex);
                // Not over the last stretch, so it eases in instead of braking hard on arrival.
                bool dart = tapWasInFront && toTap.magnitude > slowdownRadius;
                SwimTowards(destination, swimSpeed * Mathf.Lerp(tapSwimSpeedMultiplier, tapExcitedSpeedMultiplier, quickness),
                    dart, tapAccelerationMultiplier, quickness);
                // Hover there a moment, as if checking out the finger on the glass.
                if (Arrived(destination) || stateTimer <= 0f || Stuck()) EnterIdle();
                break;
            case State.Flee:
                // C-start: whip round on the spot, then bolt flat out, gliding out over the last stretch.
                targetEffort = 1f;
                Vector3 toSafety = destination - transform.position;
                // Once it has whipped round it keeps bolting, even if the heading wobbles back a little.
                bolting |= Vector3.Dot(transform.forward, toSafety.normalized) > 0.5f;
                SwimTowards(destination, swimSpeed * fleeSpeedMultiplier,
                    bolting && toSafety.magnitude > slowdownRadius, 4f, 1f); // then glides to a stop
                if (Arrived(destination) || stateTimer <= 0f)
                {
                    EnterIdle();
                    stateTimer += startleShyTime;     // holds still a bit longer, wary
                }
                break;
            case State.Idle:
                targetEffort = 0.1f;
                SwimTowards(transform.position + transform.forward, 0f);
                if (stateTimer <= 0f) EnterWander();
                break;
            case State.SeekFood:
                if (food == null) { LookForFood(); if (food == null) EnterWander(); break; }
                targetEffort = 1f;
                Vector3 target = ClampToBounds(food.transform.position, SurfaceTop(), FeedBottom());
                Vector3 toFood = target - transform.position;
                bool strike = toFood.magnitude < strikeRadius && Vector3.Dot(transform.forward, toFood.normalized) > 0.7f;
                SwimTowards(target, swimSpeed * (strike ? strikeSpeedMultiplier : seekSpeedMultiplier), strike);
                // Measured from the mouth, a little ahead of the pivot (which sits at the gills).
                Vector3 mouth = transform.position + transform.forward * mouthOffset;
                float foodDistance = (food.transform.position - mouth).magnitude;
                if (Arrived(target) || foodDistance <= arrivalRadius) { EnterEat(); break; }
                // No closer for a while: it lies where the body can't fit, leave it be.
                if (foodDistance < closestFoodDistance - 0.03f) { closestFoodDistance = foodDistance; seekStuckTimer = 0f; }
                else if ((seekStuckTimer += Time.deltaTime) > seekGiveUpTime)
                {
                    food.Unreachable = true;
                    food = null;
                    LookForFood();
                    if (food == null) EnterWander();
                }
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
                        stateTimer = Mathf.Max(gulpDuration, GulpClipSeconds);
                        if (animator) animator.SetTrigger(GulpId);
                        Gulped?.Invoke();
                    }
                }
                else
                {
                    // Hold the snout tipped up into the surface while gulping.
                    targetEffort = 0.2f;
                    SwimTowards(transform.position + Vector3.up * 0.1f + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 0.2f, 0f);
                    if (stateTimer > 0f) break;
                    // Bettas often blow a bubble or two at the surface (nest building) before diving.
                    if (!blowingBubbles && Random.value < bubbleBlowChance)
                    {
                        blowingBubbles = true;
                        stateTimer = bubbleBlowDuration;
                        if (animator) animator.SetTrigger(BubbleId);
                        BlowingBubbles?.Invoke();
                    }
                    else EnterDive();
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
        floor = state == State.Rest && restOnBottom ? Mathf.Min(normalBottom, bottomRestY)
            : state == State.SeekFood || state == State.Eat ? FeedBottom()
            : Mathf.Min(normalBottom, Mathf.Max(floor, transform.position.y));
        transform.position = ClampToBounds(transform.position, ceiling, floor);
        ResolveContacts();
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
        if (pellet != food) { closestFoodDistance = float.MaxValue; seekStuckTimer = 0f; }
        food = pellet;
        state = State.SeekFood;
    }

    // Swim over to a point in the tank (a tap on the glass). Food and a gulp in progress come first.
    public void SwimTo(Vector3 point)
    {
        tapExcitement = Mathf.Clamp01(tapExcitement + tapExcitementPerTap);
        // Food comes first; a gulp of air is only cut short by insistent tapping.
        // Just after a fright it won't come over.
        if (state == State.SeekFood || state == State.Eat || state == State.Flee || Time.time < shyUntil) return;
        if (state == State.Breathe && gulping && tapExcitement < 0.6f) return;
        state = State.SwimToTap;
        settled = false;
        destination = AboveObstacles(ClampToBounds(point));
        tapWasInFront = Vector3.Dot(transform.forward, (destination - transform.position).normalized) > tapAheadCone;
        stateTimer = 4f + Vector3.Distance(transform.position, destination) / Mathf.Max(0.05f, swimSpeed);
    }

    // A tap right on the fish: it bolts to the side of the tank away from the finger
    // (or the roomier side when the tap is dead centre), diving a little.
    public void Startle(Vector3 tapPoint)
    {
        if (state == State.Eat) return;
        food = null;
        tapExcitement = 0f;
        shyUntil = Time.time + startleShyTime + 1.5f;
        Bounds b = swimBounds ? swimBounds.bounds : new Bounds(transform.position, Vector3.one);
        Vector3 here = transform.position;
        float side = here.x - tapPoint.x;
        // Away from the finger along the length of the tank.
        float dir = Mathf.Abs(side) > 0.03f ? Mathf.Sign(side) : (here.x < b.center.x ? 1f : -1f);
        float room = dir > 0f ? b.max.x - boundsInset - here.x : here.x - (b.min.x + boundsInset);
        if (room < fleeDistance * 0.5f) dir = -dir;   // cornered: dart past the finger to the open end
        Vector3 target = here + new Vector3(dir * fleeDistance, -fleeDive, Random.Range(-0.1f, 0.1f));
        state = State.Flee;
        bolting = false;
        settled = false;
        gulping = false;
        destination = AboveObstacles(ClampToBounds(target));
        stateTimer = 3f;
    }

    // Top of the solid decoration under this spot, so sinking food lands on rocks and wood.
    public float GroundHeight(Vector3 position)
    {
        ScanObstacles();
        float top = float.MinValue;
        foreach (var col in obstacles)
        {
            Bounds b = col.bounds;
            if (position.x >= b.min.x && position.x <= b.max.x && position.z >= b.min.z && position.z <= b.max.z)
                top = Mathf.Max(top, b.max.y);
        }
        return top;
    }

    void LookForFood()
    {
        FoodPellet nearest = feeding ? feeding.NearestPellet(transform.position) : null;
        if (nearest != null) Seek(nearest);
    }

    // quickness 0..1: an excited fish turns on its pectorals and only slows right at the target.
    void SwimTowards(Vector3 target, float targetSpeed, bool strike = false, float accelerationBoost = 1f, float quickness = 0f)
    {
        Vector3 aim = targetSpeed > 0f ? Detour(target) : target;
        Vector3 direction = aim - transform.position;
        // Measured the whole way round, so it doesn't slow down as if arriving at the detour corner.
        if (aim != target) direction = direction.normalized * (direction.magnitude + Vector3.Distance(aim, target));
        if (targetSpeed > 0f) direction = AvoidObstacles(direction);
        // Heading the fish can actually swim: yaw toward the target, pitch within the
        // limit for this state. Yaw and pitch are steered separately and rebuilt with zero
        // roll; slerping the whole rotation takes a shortcut that banks the fish mid-turn.
        float targetYaw = yaw, targetPitch = 0f;
        bool tooSteep = false;
        float steepness = 0f;                     // rise or drop per metre across, when steeper than the pitch limit
        float horizontal = new Vector2(direction.x, direction.z).magnitude;
        if (direction.sqrMagnitude > 0.0001f)
        {
            // Straight above/below the heading is meaningless; keep the current one. The further up or
            // down the target, the wider that cone, so passing just under it doesn't spin the fish round.
            if (horizontal > Mathf.Max(0.03f, Mathf.Abs(direction.y) * 0.15f)) targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float limit = state == State.SeekFood ? feedMaxPitch : state == State.SwimToTap || state == State.Flee ? tapMaxPitch
                : state == State.Rest ? restMaxPitch : state == State.Breathe ? breatheMaxPitch : maxPitch;
            if (climbingObstacle) limit = Mathf.Max(limit, climbMaxPitch);
            if (targetSpeed > 0f || state == State.Breathe)
            {
                float rawPitch = -Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
                targetPitch = Mathf.Clamp(rawPitch, -limit, limit);
                tooSteep = Mathf.Abs(rawPitch) > limit && horizontal < 0.2f;
                if (Mathf.Abs(rawPitch) > limit) steepness = Mathf.Abs(direction.y) / Mathf.Max(0.01f, horizontal);
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
            // A fast fish starts easing off early enough to stop without slamming on its pectorals.
            // Scaled by the requested speed, not the current one, which would feed back into itself.
            float approach = Mathf.Clamp01(direction.magnitude / Mathf.Max(slowdownRadius * (1f - 0.6f * quickness), targetSpeed * 0.5f));
            targetSpeed *= Mathf.Max(minTurnSpeedFactor, alignment * alignment) * Mathf.Max(0.2f, approach);
            braking = approach < 1f || alignment < 0.6f;
            // Coming out of a big turn the tail sweeps through and pushes the fish off.
            // Not on the final approach, where it would arrive too fast and stop with a jolt.
            if (turnExitTimer > 0f && approach >= 1f)
            {
                targetSpeed *= turnExitBoost;
                braking = false;
            }
            // A betta never stands still and spins toward a new heading: while the heading
            // is still well off it glides forward a little and turns along that glide.
            float offHeading = Mathf.Clamp01((Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) - 15f) / 30f);
            targetSpeed = Mathf.Max(targetSpeed, turnGlideSpeed * offHeading);
        }
        // Target steeper than it may tilt: travel across more slowly, so the climb or dive (helped
        // on the pectorals below) gets there before it passes underneath or over the spot.
        if (steepness > 0f && targetSpeed > 0f)
            targetSpeed *= Mathf.Clamp(Mathf.Tan(Mathf.Abs(targetPitch) * Mathf.Deg2Rad) / steepness, 0.15f, 1f);
        // A strike is a short dart; the fish then brakes hard with its pectorals.
        float rate = targetSpeed > currentSpeed ? acceleration * (strike ? 4f : accelerationBoost)
            : braking ? acceleration * (currentSpeed > swimSpeed * 1.2f ? 3f : 1f) : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);

        // The head swings quickly toward the new heading and eases off as it lines up.
        // The rate is signed and acceleration-limited, so the swing starts and stops smoothly
        // and never jumps when the target changes side.
        float yawError = Mathf.DeltaAngle(yaw, targetYaw);
        // Target nearly straight behind: either way round will do, but it must stick to one.
        // Otherwise the error flips between +179 and -179 and the head wags on the spot.
        // Keep turning the way it already swings, or start toward the roomier side of the tank.
        if (Mathf.Abs(yawError) > 150f)
        {
            float way = Mathf.Abs(yawVelocity) > 1f ? Mathf.Sign(yawVelocity)
                : swimBounds && Vector3.Dot(transform.right, swimBounds.bounds.center - transform.position) < 0f ? -1f : 1f;
            if (Mathf.Sign(yawError) != way) yawError += 360f * way;
        }
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
            maxRate = Mathf.Max(maxRate, quickTurnRate * quickness);
            // Nosed into the glass there is no room to glide round an arc: it pivots on its pectorals.
            if (NosedIntoWall()) maxRate = Mathf.Max(maxRate, hoverHeadTurnRate);
            // Tail up against the glass: swing round slowly while backing off it, so the
            // body eases away from the wall instead of being shoved across.
            maxRate *= Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(wallContact / Mathf.Max(0.01f, BodyRadius)));
            desiredYawVelocity = Mathf.Clamp(yawError * headTurnGain, -maxRate, maxRate);
        }
        yawVelocity = Mathf.MoveTowards(yawVelocity, desiredYawVelocity, headTurnAcceleration * (1f + 2f * quickness) * Time.deltaTime);
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
        // Food or a tap well above or below: keep climbing or diving through the turn, spiralling to it
        // instead of circling level at the same height and never getting closer.
        if ((state == State.SeekFood || state == State.SwimToTap || state == State.Flee) && direction.sqrMagnitude > 0.0001f)
            turnFactor = Mathf.Max(turnFactor, Mathf.Abs(direction.y) / direction.magnitude);
        // An excited fish tips its head up or down as quickly as it swings round.
        pitch = Mathf.MoveTowards(pitch, targetPitch * turnFactor, pitchDegreesPerSecond * (1f + 2f * quickness) * Time.deltaTime);
        // Lying on the bottom the fish settles onto one side a little.
        float targetRoll = IsResting && restOnBottom ? restRoll : 0f;
        roll = Mathf.MoveTowards(roll, targetRoll, 8f * Time.deltaTime);
        transform.rotation = Quaternion.Euler(pitch, yaw, roll);
        transform.position += transform.forward * currentSpeed * ThrustPulse * Time.deltaTime;
        // Target steeper than it may tilt: rise or sink on the pectorals for the rest of the slope, so
        // it heads straight there instead of passing it and circling up or down around it.
        // Almost straight above/below it does so even when barely moving.
        if (steepness > 0f)
        {
            Vector3 velocity = transform.forward * currentSpeed * ThrustPulse;
            float needed = new Vector2(velocity.x, velocity.z).magnitude * steepness * Mathf.Sign(direction.y);
            float assist = Mathf.Clamp(needed - velocity.y, -steepClimbAssist, steepClimbAssist);
            if (assist * direction.y < 0f) assist = 0f;
            // An excited fish rises briskly toward a tap overhead rather than drifting up.
            float hoverRise = Mathf.Max(verticalHoverSpeed, steepClimbAssist * quickness);
            if (tooSteep && Mathf.Abs(assist) < hoverRise) assist = hoverRise * Mathf.Sign(direction.y);
            transform.position += Vector3.up * Mathf.Clamp(direction.y, -Mathf.Abs(assist) * Time.deltaTime, Mathf.Abs(assist) * Time.deltaTime);
        }

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

    // The pivot is held at the swim bounds and the fish faces further out: swimming on gets it nowhere.
    bool NosedIntoWall()
    {
        Vector3 here = transform.position;
        Vector3 ahead = here + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 0.05f;
        Vector3 clamped = ClampToBounds(ahead);
        return new Vector2(clamped.x - ahead.x, clamped.z - ahead.z).sqrMagnitude > 0.0004f;
    }

    // True once the fish can't get to its destination: it has barely moved for a while (wedged and
    // pushed back and forth), or got no closer for longer (sliding along decor, circling it).
    // A new destination starts the count again; a U-turn moves it far enough not to count.
    bool Stuck()
    {
        Vector3 here = transform.position;
        if (destination != stuckDestination)
        {
            stuckDestination = destination;
            stuckAnchor = here;
            closestDestinationDistance = float.MaxValue;
            stillTimer = noProgressTimer = 0f;
        }
        if ((here - stuckAnchor).sqrMagnitude > stuckMoveDistance * stuckMoveDistance) { stuckAnchor = here; stillTimer = 0f; }
        // Only while it is really swimming: easing in slowly to the spot is not being stuck.
        else if (currentSpeed > 0.05f) stillTimer += Time.deltaTime;
        float distance = (destination - here).magnitude;
        if (distance < closestDestinationDistance - 0.03f) { closestDestinationDistance = distance; noProgressTimer = 0f; }
        else noProgressTimer += Time.deltaTime;
        return stillTimer > stuckStillTime || noProgressTimer > stuckNoProgressTime;
    }

    void ChooseNextCalmState()
    {
        float dice = Random.value;
        if (dice < restChance) EnterRest();
        else if (dice < restChance + idleChance) EnterIdle();
        else EnterWander();
    }

    // escape: it got stuck, so of a few random spots pick the one lying most behind it and
    // toward the open middle of the tank, away from whatever it was wedged against.
    void EnterWander(bool escape = false)
    {
        state = State.Wander;
        Bounds b = swimBounds ? swimBounds.bounds : new Bounds(transform.position, Vector3.one);
        float y = transform.position.y + Random.Range(-wanderHeightRange, wanderHeightRange);
        Vector3 here = transform.position;
        Vector3 away = Vector3.ProjectOnPlane(-transform.forward, Vector3.up).normalized
            + Vector3.ProjectOnPlane(b.center - here, Vector3.up).normalized;
        float best = float.NegativeInfinity;
        for (int i = 0; i < (escape ? 6 : 1); i++)
        {
            Vector3 spot = AboveObstacles(ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, TowardFront(b))));
            Vector3 to = Vector3.ProjectOnPlane(spot - here, Vector3.up);
            float score = to.magnitude < 0.2f ? -10f : Vector3.Dot(to.normalized, away);
            if (score > best) { best = score; destination = spot; }
        }
        stateTimer = Random.Range(wanderMinTime, wanderMaxTime);
    }

    void EnterBreathe()
    {
        state = State.Breathe;
        gulping = false;
        blowingBubbles = false;
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
        destination = AboveObstacles(ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x), y, TowardFront(b))));
        stateTimer = Random.Range(wanderMinTime, wanderMaxTime);
    }

    float SurfaceTop() => waterSurfaceY - gulpDepth;

    float NormalBottom() => swimBounds ? swimBounds.bounds.min.y + boundsInset : float.MinValue;

    // Sinking pellets end up on the sand, below the normal swimming space.
    float FeedBottom() => Mathf.Min(NormalBottom(), bottomFeedY);

    float NormalTop() =>swimBounds ? swimBounds.bounds.max.y - boundsInset : float.MaxValue;

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
    // Nosing a pellet off a rock or driftwood top the long fins just drape over it, so they don't hold
    // the body up (otherwise the mouth stays a fin's length above the pellet and never reaches it).
    bool FinsDrape => state == State.SeekFood && food != null && food.OnBottom;

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
    // Until BettaSwimMotion reports the bent body these lie straight behind the gills.
    static readonly float[] BodySamples = { 0.15f, 0f, -0.35f, -0.7f, -1f };

    // Snout, gills, the spine as drawn, and last the tail tip. Walls and decor test the real
    // bent body: right after turning away from the glass the tail still follows the path inside,
    // where a straight line behind the new heading would poke through and shove the fish sideways.
    int BodyPointCount => BodyLine != null ? BodyLine.Length + 2 : BodySamples.Length + 1;

    Vector3 BodyPoint(int i)
    {
        Vector3 here = transform.position;
        if (BodyLine == null || i == 0)
            return here + transform.forward * ((i < BodySamples.Length ? BodySamples[i] : -tailReachBodyLengths) * BodyLength);
        return i == 1 ? here : here + BodyLine[i - 2];
    }

    // Stone and wood stacked on another piece leave a gap underneath that the fish would get
    // wedged in (pushed down by the piece above, up by the one below), so each obstacle counts
    // as solid all the way down: below its underside the footprint is extended straight down.
    static Vector3 ClosestObstaclePoint(Collider col, Vector3 point)
    {
        float underside = col.bounds.min.y;
        if (point.y >= underside) return col.ClosestPoint(point);
        Vector3 closest = col.ClosestPoint(new Vector3(point.x, underside, point.z));
        closest.y = point.y;
        return closest;
    }

    // Height the pivot needs for the body and hanging fins to clear this obstacle.
    float ClearHeight(Collider col) => col.bounds.max.y + (FinsDrape ? 0f : FinHang) + BodyRadius;

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

    // The straight way to the target runs into decor it has to go round (too tall to swim over):
    // aim for the near corner of it first. Steering away only once the snout is at the rock gets
    // cancelled by the pull toward the target, and the fish keeps nosing into the rock.
    // Corner by corner round the footprint, keeping to the side it chose for that piece.
    Vector3 Detour(Vector3 target)
    {
        ScanObstacles();
        Vector3 here = transform.position;
        float pad = BodyRadius + obstacleClearance + 0.05f;
        Bounds swim = swimBounds ? swimBounds.bounds : new Bounds(here, Vector3.one * 100f);
        for (int pass = 0; pass < 3; pass++)
        {
            Vector2 from = new Vector2(here.x, here.z), to = new Vector2(target.x, target.z);
            Collider blocking = null;
            Rect blockingRect = default;
            float nearest = float.MaxValue;
            foreach (var col in obstacles)
            {
                if (ClimbOver(col) || Mathf.Min(here.y, target.y) > ClearHeight(col)) continue;
                Bounds b = col.bounds;
                Rect rect = Rect.MinMaxRect(b.min.x - pad, b.min.z - pad, b.max.x + pad, b.max.z + pad);
                // A target right beside it is left to the close-range steering.
                if (rect.Contains(to)) continue;
                // Already rounding it (inside the margin): blocked only if the way on cuts into the piece
                // itself, or every way out would count and it would carry on round past the target.
                Rect test = rect.Contains(from) ? Rect.MinMaxRect(b.min.x - BodyRadius, b.min.z - BodyRadius, b.max.x + BodyRadius, b.max.z + BodyRadius) : rect;
                if (SegmentEntersRect(from, to, test, out float enter) && enter < nearest)
                {
                    nearest = enter;
                    blocking = col;
                    blockingRect = rect;
                }
            }
            if (blocking == null) return target;

            // The outermost corner on each side of the line, a little further out.
            Vector2 line = (to - from).normalized;
            Vector2 left = default, right = default;
            float mostLeft = float.NegativeInfinity, mostRight = float.PositiveInfinity;
            foreach (var c in new[] { blockingRect.min, blockingRect.max, new Vector2(blockingRect.xMin, blockingRect.yMax), new Vector2(blockingRect.xMax, blockingRect.yMin) })
            {
                float across = line.x * (c.y - from.y) - line.y * (c.x - from.x);
                if (across > mostLeft) { mostLeft = across; left = c; }
                if (across < mostRight) { mostRight = across; right = c; }
            }
            left += (left - blockingRect.center).normalized * 0.05f;
            right += (right - blockingRect.center).normalized * 0.05f;
            bool leftOk = Inside(left), rightOk = Inside(right);
            if (!leftOk && !rightOk) return target;
            // Which way round: the side of its path the piece stays on (sign of the cross product).
            // Remembered that way, not as left or right of the line to the target, which turns
            // as the fish goes round.
            float leftWay = Mathf.Sign(Cross(left - from, blockingRect.center - from));
            bool takeLeft = blocking == detourAround && (leftWay == detourSide ? leftOk : rightOk) ? leftWay == detourSide
                : !rightOk || leftOk && (left - from).magnitude + (to - left).magnitude <= (right - from).magnitude + (to - right).magnitude;
            detourAround = blocking;
            detourSide = takeLeft ? leftWay : -leftWay;
            Vector2 corner = takeLeft ? left : right;
            float share = (corner - from).magnitude / Mathf.Max(0.01f, (corner - from).magnitude + (to - corner).magnitude);
            target = new Vector3(corner.x, Mathf.Lerp(here.y, target.y, share), corner.y);
        }
        return target;

        bool Inside(Vector2 p) => p.x > swim.min.x + boundsInset && p.x < swim.max.x - boundsInset
            && p.y > swim.min.z + boundsInset && p.y < swim.max.z - boundsInset;
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }

    // Liang-Barsky: whether the segment enters the rect, and how far along (0 when it starts inside).
    static bool SegmentEntersRect(Vector2 from, Vector2 to, Rect rect, out float enter)
    {
        enter = 0f;
        float exit = 1f;
        Vector2 d = to - from;
        for (int axis = 0; axis < 2; axis++)
        {
            float o = from[axis], v = d[axis], min = rect.min[axis], max = rect.max[axis];
            if (Mathf.Abs(v) < 1e-6f)
            {
                if (o < min || o > max) return false;
                continue;
            }
            float t0 = (min - o) / v, t1 = (max - o) / v;
            if (t0 > t1) (t0, t1) = (t1, t0);
            enter = Mathf.Max(enter, t0);
            exit = Mathf.Min(exit, t1);
            if (enter > exit) return false;
        }
        return true;
    }

    // Steers the swim direction around or over decorations ahead.
    Vector3 AvoidObstacles(Vector3 direction)
    {
        ScanObstacles();
        climbingObstacle = false;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 snout = transform.position + transform.forward * (BodySamples[0] * BodyLength);
        float r = BodyRadius, reach = r + obstacleClearance;

        // Over: rise early enough to clear what lies ahead, and hold the height until the
        // tail and its fins have passed (the stretch behind the snout is checked too).
        // Picking food off the bottom it dips nose-down with the tail held high, so only
        // what lies ahead counts; ResolveContacts still pushes the tilted body out of decor.
        float trailFrom = state == State.SeekFood && food != null && food.OnBottom ? 0f : -BodyLength * 1.15f;
        float slope = float.NegativeInfinity;
        foreach (var col in obstacles)
        {
            if (!ClimbOver(col)) continue;
            Bounds b = col.bounds;
            float needed = ClearHeight(col) - transform.position.y;
            for (float d = trailFrom; d <= climbLookAhead; d += 0.05f)
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
                    Vector3 away = point - ClosestObstaclePoint(col, point);
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

        // Walls: turn away before the snout reaches the glass or stone. Only checked up to the
        // target, so a spot close to a wall can still be reached.
        if (swimBounds)
        {
            Bounds swim = swimBounds.bounds;
            float margin = wallClearanceBodyLengths * BodyLength;
            // Same limits as WallPush: the open front glass may be brushed.
            Vector3 min = new Vector3(swim.min.x + margin, 0f, swim.min.z + .02f);
            Vector3 max = new Vector3(swim.max.x - margin, 0f, swim.max.z - margin);
            for (int k = 1; k <= 3; k++)
            {
                float d = wallLookAhead * k / 3f;
                if (d > direction.magnitude) break;
                Vector3 p = snout + forward * d;
                steer += WallSteer(p.x - min.x, Vector3.right) / k + WallSteer(max.x - p.x, Vector3.left) / k
                    + WallSteer(p.z - min.z, Vector3.forward) / k + WallSteer(max.z - p.z, Vector3.back) / k;
            }

            // gap: room left between the point and the wall; normal: points away from the wall.
            // Straight off the wall in world space, not sideways to the heading: in a corner the
            // two walls then add up to "out of the corner" instead of pulling the head left and
            // right in turn as it swings. A push straight back is a U-turn, which SwimTowards
            // keeps turning one way.
            Vector3 WallSteer(float gap, Vector3 normal) =>
                gap >= wallAvoidDistance ? Vector3.zero : normal * Mathf.Min(2f, 1f - gap / wallAvoidDistance);
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

    // Last resort: ease the body back out of the decor and walls it still overlaps. Decor and wall
    // pushes are added up and applied once, so wedged between a rock and the glass the fish settles
    // between them instead of being shoved back and forth.
    void ResolveContacts()
    {
        Vector3 wallPush = WallPush(0f);
        wallContact = wallPush.magnitude;
        Vector3 push = ObstaclePush() + wallPush;
        if (push != Vector3.zero)
            transform.position = ClampToBounds(transform.position + push * Mathf.Clamp01(contactPushRate * Time.deltaTime), SurfaceTop(), float.MinValue);
        // Never deeper than this into the walls, however fast it turned.
        transform.position += WallPush(maxWallPenetration);
    }

    // Per obstacle the largest push any body point needs; summed over obstacles.
    Vector3 ObstaclePush()
    {
        ScanObstacles();
        Vector3 total = Vector3.zero;
        foreach (var col in obstacles) total += ObstaclePush(col);
        return total;
    }

    Vector3 ObstaclePush(Collider col)
    {
        Vector3 push = Vector3.zero;
        for (int i = 0; i < BodyPointCount - 1; i++)
        {
            Vector3 body = BodyPoint(i);
            for (int f = 0; f < (i == 0 || FinsDrape ? 1 : 2); f++)
            {
                Vector3 point = f == 0 ? body : body + Vector3.down * FinHang;
                Vector3 closest = ClosestObstaclePoint(col, point);
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
        return push;
    }

    // Lifts a spot so the fish (and its hanging fins) clears any decoration under it.
    // Next to a low piece too, within the reach of the body and fins: down in the slot beside a
    // rock (or behind it, against the glass) the long-finned fish can't get in from above, and
    // would circle over the rock instead; it hovers over the rock there. Beside a tall piece the
    // spot stays where it is, and the fish goes round.
    Vector3 AboveObstacles(Vector3 position)
    {
        ScanObstacles();
        foreach (var col in obstacles)
        {
            Bounds b = col.bounds;
            float r = b.max.y + FinHang + BodyRadius <= NormalTop() ? BodyRadius + FinHang : BodyRadius;
            if (position.x < b.min.x - r || position.x > b.max.x + r || position.z < b.min.z - r || position.z > b.max.z + r) continue;
            position.y = Mathf.Max(position.y, Mathf.Min(b.max.y + FinHang + BodyRadius, NormalTop()));
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

    // Somewhere across the depth of the tank, more often toward the front glass where the player sees it best.
    static float TowardFront(Bounds b) => Mathf.Lerp(b.min.z, b.max.z, Mathf.Pow(Random.value, 1.6f));

    // The pivot is clamped above; this is how far it must move away from the side, back and front
    // glass for the snout, the body and the tail tip, with their sideways sway, to be inside too.
    // allowance: overlap that is left alone.
    Vector3 WallPush(float allowance)
    {
        if (swimBounds == null) return Vector3.zero;
        Bounds b = swimBounds.bounds;
        float margin = wallClearanceBodyLengths * BodyLength - allowance;
        float pushX = 0f, pushZ = 0f;
        for (int i = 0; i < BodyPointCount; i++)
        {
            Vector3 p = BodyPoint(i);
            pushX = Push(pushX, p.x, b.min.x + margin, b.max.x - margin);
            // Only the stone walls need the clearance; at the open front the fish may brush the glass.
            pushZ = Push(pushZ, p.z, b.min.z + .02f - allowance, b.max.z - margin);
        }
        return new Vector3(pushX, 0f, pushZ);

        // The largest push needed in either direction; a fish longer than the tank stays centred.
        static float Push(float push, float value, float min, float max)
        {
            if (min > max) return push;
            if (value < min) return Mathf.Max(push, min - value);
            if (value > max) return Mathf.Min(push, max - value);
            return push;
        }
    }
}
