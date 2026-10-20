using UnityEngine;

// Lightweight FSM from the GDD: Wander, Idle, SeekFood, Eat, Rest.
// The fish always swims along its own forward axis and turns gradually,
// so it never slides sideways or snaps orientation.
public class BettaFishAI : MonoBehaviour
{
    public enum State { Wander, Idle, SeekFood, Eat, Rest }
    public State state = State.Wander;
    public BoxCollider swimBounds;
    public FeedingSystem feeding;
    public Animator animator;

    [Header("Movement")]
    public float swimSpeed = 0.3f;
    public float seekSpeedMultiplier = 1.6f;
    public float acceleration = 0.6f;
    public float turnDegreesPerSecond = 90f;
    public float maxPitch = 25f;
    public float arrivalRadius = 0.12f;
    public float slowdownRadius = 0.5f;
    public float boundsInset = 0.15f;

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

    void Start() { EnterWander(); }

    void Update()
    {
        if (swimBounds == null) return;
        hunger = Mathf.Clamp01(hunger + hungerPerSecond * Time.deltaTime);
        stateTimer -= Time.deltaTime;

        if (state != State.SeekFood && state != State.Eat) LookForFood();

        switch (state)
        {
            case State.Wander:
                SwimTowards(destination, swimSpeed);
                if (stateTimer <= 0f || Arrived(destination)) ChooseNextCalmState();
                break;
            case State.Idle:
            case State.Rest:
                SwimTowards(transform.position + transform.forward, 0f);
                if (stateTimer <= 0f) EnterWander();
                break;
            case State.SeekFood:
                if (food == null) { LookForFood(); if (food == null) EnterWander(); break; }
                Vector3 target = ClampToBounds(food.transform.position);
                SwimTowards(target, swimSpeed * seekSpeedMultiplier);
                if (Arrived(target)) EnterEat();
                break;
            case State.Eat:
                SwimTowards(transform.position + transform.forward, 0f);
                if (stateTimer <= 0f)
                {
                    if (food != null)
                    {
                        food.Consume();
                        hunger = Mathf.Clamp01(hunger - hungerPerPellet);
                    }
                    food = null;
                    LookForFood();
                    if (food == null) EnterIdle();
                }
                break;
        }

        transform.position = ClampToBounds(transform.position);
        if (animator) animator.SetFloat(SpeedId, currentSpeed);
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

    void SwimTowards(Vector3 target, float targetSpeed)
    {
        Vector3 direction = target - transform.position;
        if (targetSpeed > 0f)
        {
            // Slow down near the target and while turning, so the turn radius
            // stays smaller than arrivalRadius and the fish can't orbit forever.
            float alignment = Vector3.Dot(transform.forward, direction.normalized) * 0.5f + 0.5f;
            float approach = Mathf.Clamp01(direction.magnitude / slowdownRadius);
            targetSpeed *= Mathf.Max(0.15f, alignment * alignment) * Mathf.Max(0.2f, approach);
        }
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 euler = look.eulerAngles;
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -maxPitch, maxPitch);
            look = Quaternion.Euler(pitch, euler.y, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnDegreesPerSecond * Time.deltaTime);
        }
        // Ease back to level when idle so the fish doesn't hang nose-up.
        if (targetSpeed <= 0f)
        {
            Quaternion level = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, level, turnDegreesPerSecond * 0.3f * Time.deltaTime);
        }
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    bool Arrived(Vector3 target) => (target - transform.position).sqrMagnitude <= arrivalRadius * arrivalRadius;

    void ChooseNextCalmState()
    {
        float roll = Random.value;
        if (roll < restChance) { state = State.Rest; stateTimer = restDuration; }
        else if (roll < restChance + idleChance) EnterIdle();
        else EnterWander();
    }

    void EnterWander()
    {
        state = State.Wander;
        Bounds b = swimBounds ? swimBounds.bounds : new Bounds(transform.position, Vector3.one);
        destination = ClampToBounds(new Vector3(Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z)));
        stateTimer = Random.Range(wanderMinTime, wanderMaxTime);
    }

    void EnterIdle()
    {
        state = State.Idle;
        stateTimer = Random.Range(idleMinTime, idleMaxTime);
    }

    void EnterEat()
    {
        state = State.Eat;
        stateTimer = eatDuration;
        if (animator) animator.SetTrigger(NibbleId);
    }

    Vector3 ClampToBounds(Vector3 position)
    {
        if (swimBounds == null) return position;
        Bounds b = swimBounds.bounds;
        return new Vector3(Mathf.Clamp(position.x, b.min.x + boundsInset, b.max.x - boundsInset),
            Mathf.Clamp(position.y, b.min.y + boundsInset, b.max.y - boundsInset),
            Mathf.Clamp(position.z, b.min.z + boundsInset, b.max.z - boundsInset));
    }
}
