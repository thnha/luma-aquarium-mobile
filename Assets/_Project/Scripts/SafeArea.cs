using UnityEngine;

// Fits this RectTransform to Screen.safeArea so UI stays clear of notches and home indicators.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private Rect applied;
    private Vector2Int screenSize;

    void Update()
    {
        if (applied == Screen.safeArea && screenSize.x == Screen.width && screenSize.y == Screen.height) return;
        applied = Screen.safeArea;
        screenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
        rt.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
