using UnityEngine;

// Routes the existing aquarium AI's animation commands to an imported fish's fitted rig.
[DefaultExecutionOrder(-40)]
public sealed class BettaRiggedModel : MonoBehaviour
{
    public Animator animator;
    public BettaReactions reactions;
    BettaFishAI fish;
    Animator previousAnimator;

    void OnEnable()
    {
        fish = GetComponentInParent<BettaFishAI>();
        if (!fish) return;
        previousAnimator = fish.animator;
        fish.animator = animator;
        if (reactions)
        {
            reactions.fish = fish;
            reactions.animator = animator;
            var previous = fish.GetComponent<BettaReactions>();
            if (previous) reactions.viewCamera = previous.viewCamera;
        }
    }

    void OnDisable()
    {
        if (fish && fish.animator == animator) fish.animator = previousAnimator;
    }
}
