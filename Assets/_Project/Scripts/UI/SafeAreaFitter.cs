using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform target;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        target = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void OnEnable()
    {
        if (target == null)
        {
            target = GetComponent<RectTransform>();
        }

        ApplySafeArea();
    }

    private void Update()
    {
        var screenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.safeArea != lastSafeArea || screenSize != lastScreenSize)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        if (target == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        var safeArea = Screen.safeArea;
        var anchorMin = safeArea.position;
        var anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
