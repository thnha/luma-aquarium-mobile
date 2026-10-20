using UnityEngine;
using UnityEngine.UI;
public class FishStatusUI : MonoBehaviour
{
    public BettaFishAI fish;
    public Slider hungerSlider;
    void Update()
    {
        if (fish != null && hungerSlider != null) hungerSlider.value = fish.hunger;
    }
}
