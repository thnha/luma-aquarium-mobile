using UnityEngine;

// Fits this RectTransform to Screen.safeArea so UI stays clear of notches and home indicators.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private Rect applied;
    private Vector2Int screenSize;

    void Update()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Rect area = Screen.safeArea;
        // Device Simulator can leave a previous device's safe area while the Game view resizes.
        // Use the current viewport until Unity reports a safe area that belongs to it.
        if (area.width <= 0 || area.height <= 0 || area.xMin < 0 || area.yMin < 0
            || area.xMax > Screen.width || area.yMax > Screen.height)
            area = new Rect(0, 0, Screen.width, Screen.height);
        if (applied == area && screenSize.x == Screen.width && screenSize.y == Screen.height) return;
        applied = area;
        screenSize = new Vector2Int(Screen.width, Screen.height);

        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
        rt.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
