using System.Collections.Generic;
using UnityEngine;

// iPhone/iPad: keeps a code-built canvas out of the notch, rounded corners and home bar by moving its
// children into a panel sized to Screen.safeArea. Added by UIKit.MakeCanvas on mobile only.
public class SafeArea : MonoBehaviour
{
    private RectTransform panel;
    private Rect applied;
    private readonly List<Transform> moving = new List<Transform>();

    // LateUpdate, so UI built or added this frame is moved before it is drawn.
    private void LateUpdate()
    {
        if (panel == null)
        {
            panel = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(transform, false);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
        }
        moving.Clear();
        foreach (Transform child in transform) if (child != panel) moving.Add(child);
        foreach (Transform child in moving) child.SetParent(panel, false); // keeps draw order and each child's anchors

        Rect safe = Screen.safeArea;
        if (safe == applied || Screen.width == 0 || Screen.height == 0) return;
        applied = safe;
        Vector2 size = new Vector2(Screen.width, Screen.height);
        panel.anchorMin = safe.position / size;
        panel.anchorMax = (safe.position + safe.size) / size;
    }
}
