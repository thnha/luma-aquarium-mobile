using UnityEngine;
public class MobileSettings : MonoBehaviour
{
    void Awake()
    {
        Screen.orientation = ScreenOrientation.Portrait;
        Application.targetFrameRate = 30;
        QualitySettings.vSyncCount = 0;
    }
}
