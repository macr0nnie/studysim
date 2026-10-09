using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Everything the character does besides animate: uses furniture (sit when studying, lie on a break), keeps a hidden
// happiness index, shows a mood icon over its head and says occasional lines in a speech bubble.
// Added to the character by StudyCharacter, so a hand-placed Player gets it too.
public class CharacterBehaviour : MonoBehaviour
{
    private const float SayEvery = 30f, SayFor = 4f;

    // Hidden from the player: 0..100, drifts toward what the room deserves.
    public float Happiness { get; private set; } = 50f;
    public Mood Mood { get; private set; } = Mood.Content;

    private RoomManager room;
    private TimerManager timer;
    private Animator animator;
    private Renderer[] renderers;
    private SpriteRenderer icon;
    private SpriteRenderer bubbleBack;
    private TextMeshPro bubbleText;
    private MoodIcons icons;
    private float nextSay, hideBubble, nextCheck;
    private GameObject usedPiece;
    private bool lying;

    private void Start()
    {
        room = FindFirstObjectByType<RoomManager>();
        timer = FindFirstObjectByType<TimerManager>();
        animator = GetComponent<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        icons = Resources.Load<MoodIcons>("MoodIcons");
        standingHeight = Mathf.Max(0.1f, BoundsOf(gameObject).size.y);
        Happiness = Target();
        nextSay = Time.time + 8f;
        MakeOverlays();
    }

    private void Update()
    {
        // The target moves when a piece is bought, so it is read each frame; distraction drops it faster than the room lifts it.
        float target = Target();
        Happiness = Mathf.MoveTowards(Happiness, target, (target < Happiness ? 6f : 1.5f) * Time.deltaTime);
        Mood = Distraction.Reported ? Mood.Stressed : Happiness >= 70f ? Mood.Happy : Happiness < 30f ? Mood.Sad : Mood.Content;

        if (Time.time >= nextCheck) { nextCheck = Time.time + 0.5f; UseFurniture(); }
        if (Time.time >= nextSay && !lying) { Say(CharacterDialogue.Line(Mood, CharacterDialogue.Stage)); nextSay = Time.time + SayEvery * Random.Range(0.8f, 1.6f); }
        if (bubbleBack.gameObject.activeSelf && Time.time >= hideBubble) bubbleBack.gameObject.SetActive(false);
        if (usedPiece != null) Hold();
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || icon == null) return;
        Bounds b = default; bool found = false;
        foreach (Renderer r in renderers) if (r != null && r.enabled && !(r is SpriteRenderer) && !(r is TextMeshPro)) { if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds); }
        if (!found) return;
        float h = b.size.magnitude * 0.2f;
        Vector3 top = new Vector3(b.center.x, b.max.y + h * 0.5f, b.center.z);
        Place(icon.transform, top, h * 0.6f, cam);
        Place(bubbleBack.transform, top + Vector3.up * h * 1.1f, h * 0.35f, cam);
    }

    private static void Place(Transform t, Vector3 pos, float size, Camera cam)
    {
        t.position = pos;
        t.rotation = cam.transform.rotation;
        t.localScale = Vector3.one * size;
    }

    // Happiness the room currently deserves: a baseline plus each piece's comfort, minus a big hit while distracted.
    private float Target()
    {
        float t = 30f;
        if (room != null)
            foreach (GameObject piece in room.PlacedPieces)
            {
                if (piece == null || !piece.activeInHierarchy) continue;
                FurnitureItem item = room.ItemOf(piece);
                t += 2f * (item != null ? item.comfort : 1);
            }
        if (Distraction.Reported) t -= 50f;
        return Mathf.Clamp(t, 0f, 100f);
    }

    public void Say(string line)
    {
        if (string.IsNullOrEmpty(line) || bubbleText == null) return;
        bubbleText.text = line;
        bubbleBack.gameObject.SetActive(true);
        hideBubble = Time.time + SayFor;
    }

    // ---------- furniture ----------

    private Matrix4x4 Rest(Transform t)
    {
        Distraction.RestPose(t, out Vector3 p, out Quaternion r);
        return Matrix4x4.TRS(p, r, t.lossyScale);
    }

    // The old scene's furniture isn't all store items, so names count too.
    private static bool Named(GameObject g, string part) => FurnitureRole.NameHas(g, part);

    private bool Is(GameObject g, string role) => FurnitureRole.Is(g, room.ItemOf(g), role);

    private bool Usable(GameObject p, out Interaction data)
    {
        FurnitureItem item = room.ItemOf(p);
        data = item == null ? default : item.lie;
        if (data.enabled) return true;
        if (Named(p, "bed") && !Named(p, "plane")) { data.enabled = true; return true; }
        return false;
    }

    // Chairs: pieces named chair, or a chair inside another piece (the old desk). Each is paired with its nearest desk.
    private bool Chairs(out List<Transform> chairs)
    {
        chairs = new List<Transform>();
        foreach (GameObject p in room.PlacedPieces)
        {
            if (p == null || !p.activeInHierarchy) continue;
            FurnitureItem item = room.ItemOf(p);
            if (Is(p, "chair") || (item != null && item.sit.enabled && !Is(p, "desk"))) chairs.Add(p.transform);
            else if (Child(p, "chair") is Transform c) chairs.Add(c);
        }
        return chairs.Count > 0;
    }

    private Bounds? NearestDesk(Vector3 from)
    {
        Bounds? best = null; float bestDist = float.MaxValue;
        foreach (GameObject p in room.PlacedPieces)
        {
            if (p == null || !p.activeInHierarchy || !Is(p, "desk")) continue;
            Bounds b = BoundsOf(p);
            float d = b.SqrDistance(from);
            if (d < bestDist) { bestDist = d; best = b; }
        }
        return best;
    }

    // Sit in the chair, on the floor under it, facing the nearest desk along whichever of the chair's own horizontal
    // axes points at it most, so a turned chair is respected. False when there is no desk to pair with.
    private bool SitSpot(Transform chair, out Vector3 pos, out Quaternion rot, out float dist)
    {
        pos = default; rot = default; dist = 0f;
        Bounds b = BoundsOf(chair.gameObject);
        Bounds? desk = b.size == Vector3.zero ? null : NearestDesk(b.center);
        if (desk == null) return false;
        pos = new Vector3(b.center.x, b.min.y, b.center.z);
        Vector3 toDesk = Vector3.ProjectOnPlane(desk.Value.ClosestPoint(b.center) - pos, Vector3.up);
        dist = toDesk.magnitude;
        if (dist < 0.0001f) toDesk = Vector3.ProjectOnPlane(desk.Value.center - pos, Vector3.up);
        Vector3 face = Vector3.zero; float bestDot = -2f;
        foreach (Vector3 axis in new[] { chair.right, -chair.right, chair.up, -chair.up, chair.forward, -chair.forward })
        {
            Vector3 flat = Vector3.ProjectOnPlane(axis, Vector3.up);
            if (flat.magnitude < 0.7f) continue; // an axis pointing up or down says nothing about which way the chair faces
            float dot = Vector3.Dot(flat.normalized, toDesk.normalized);
            if (dot > bestDot) { bestDot = dot; face = flat.normalized; }
        }
        if (face == Vector3.zero) face = toDesk.sqrMagnitude > 0f ? toDesk.normalized : Vector3.back;
        rot = Quaternion.LookRotation(face);
        return true;
    }

    private static Bounds BoundsOf(GameObject g)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        return b;
    }

    private static Transform Child(GameObject g, string part)
    {
        foreach (Transform t in g.GetComponentsInChildren<Transform>()) if (Named(t.gameObject, part)) return t;
        return null;
    }

    private Bounds FloorBounds()
    {
        Bounds f = default; bool any = false;
        foreach (Renderer r in room.FloorRenderers) if (r != null) { if (!any) { f = r.bounds; any = true; } else f.Encapsulate(r.bounds); }
        return f;
    }

    // Lying on a bed: on the mattress top, along the bed's long side, head at the headboard (the tallest end), back flat.
    private bool Spot(GameObject piece, Interaction d, out Vector3 pos, out Quaternion rot)
    {
        pos = default; rot = Quaternion.identity;
        Bounds b = BoundsOf(piece);
        if (b.size == Vector3.zero) return false;
        bool alongX = b.size.x >= b.size.z;
        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        float along(Vector3 v) => alongX ? v.x : v.z;

        // Mattress = the tallest of the big flat parts (the frame is as wide but lower); headboard = the highest part overall.
        float biggest = 0f; Renderer top = null;
        foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            biggest = Mathf.Max(biggest, r.bounds.size.x * r.bounds.size.z);
            if (top == null || r.bounds.max.y > top.bounds.max.y) top = r;
        }
        float mattressTop = b.min.y;
        foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
            if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.bounds.size.x * r.bounds.size.z >= biggest * 0.5f) mattressTop = Mathf.Max(mattressTop, r.bounds.max.y);

        // shortcut: headboard = the end holding the highest part; a flat bed falls back to the end nearer a wall
        float sign;
        float lean = top != null ? along(top.bounds.center) - along(b.center) : 0f;
        if (Mathf.Abs(lean) > 0.2f * (alongX ? b.size.x : b.size.z)) sign = Mathf.Sign(lean);
        else
        {
            Bounds floor = FloorBounds();
            float c = along(b.center), lo = alongX ? floor.min.x : floor.min.z, hi = alongX ? floor.max.x : floor.max.z;
            sign = floor.size == Vector3.zero || Mathf.Abs(c - lo) < Mathf.Abs(hi - c) ? -1f : 1f;
        }
        Vector3 head = axis * sign;
        float longSide = alongX ? b.size.x : b.size.z;
        // The pivot is the feet: half a body back from the middle, then a little toward the pillow.
        Vector3 mid = new Vector3(b.center.x + d.offset.x * b.extents.x, mattressTop + d.height * b.size.y, b.center.z + d.offset.y * b.extents.z);
        pos = mid + head * (0.1f * longSide - standingHeight * 0.5f);
        rot = Quaternion.AngleAxis(d.yaw, Vector3.up) * Quaternion.LookRotation(Vector3.up, head);
        return true;
    }

    private void UseFurniture()
    {
        if (room == null || animator == null) return;
        bool wantLie = timer != null && !timer.IsStudySession;
        if (Distraction.Busy && usedPiece != null && wantLie == lying) return; // the room is shaking: keep holding the last good spot
        GameObject best = null; Vector3 pos = default; Quaternion rot = default; float bestDist = float.MaxValue;
        if (wantLie)
        {
            IReadOnlyList<GameObject> pieces = room.PlacedPieces;
            for (int i = pieces.Count - 1; i >= 0 && best == null; i--)
            {
                GameObject p = pieces[i];
                if (p != null && p.activeInHierarchy && Usable(p, out Interaction d) && Spot(p, d, out pos, out rot)) best = p;
            }
        }
        else if (Chairs(out List<Transform> chairs))
        {
            // Several chairs: the one sitting closest to a desk, the newest winning a tie.
            for (int i = chairs.Count - 1; i >= 0; i--)
                if (SitSpot(chairs[i], out Vector3 p2, out Quaternion r2, out float dist) && dist < bestDist) { bestDist = dist; best = chairs[i].gameObject; pos = p2; rot = r2; }
        }
        if (best == null) { if (usedPiece != null) { usedPiece = null; Release(); } return; }
        if (usedPiece == null || wantLie != lying)
        {
            if (wantLie) { animator.SetBool("IsStudying", false); animator.speed = 0f; } // no lying clip yet: freeze the pose
            else { animator.speed = 1f; animator.SetBool("IsStudying", true); }
        }
        usedPiece = best;
        lying = wantLie;
        // Remembered in the piece's own frame, so the character rides with it instead of drifting.
        Matrix4x4 rest = Rest(best.transform);
        localPos = rest.inverse.MultiplyPoint3x4(pos);
        localRot = Quaternion.Inverse(rest.rotation) * rot;
    }

    private Vector3 localPos;
    private Quaternion localRot;
    private float standingHeight = 1f;

    private void Hold()
    {
        Matrix4x4 rest = Rest(usedPiece.transform);
        transform.SetPositionAndRotation(rest.MultiplyPoint3x4(localPos), rest.rotation * localRot);
    }

    private void Release()
    {
        lying = false;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (animator != null) { animator.speed = 1f; animator.SetBool("IsStudying", true); }
    }

    // Debug: cyan = where the character would sit in each chair, magenta = where it would lie, with a line for facing.
    private void OnDrawGizmos()
    {
        if (room == null) return;
        if (Chairs(out List<Transform> chairs))
            foreach (Transform c in chairs)
                if (SitSpot(c, out Vector3 pos, out Quaternion rot, out _)) Mark(pos, rot * Vector3.forward, Color.cyan);
        foreach (GameObject p in room.PlacedPieces)
            if (p != null && p.activeInHierarchy && Usable(p, out Interaction d) && Spot(p, d, out Vector3 pos, out Quaternion rot)) Mark(pos, rot * Vector3.up, Color.magenta);
    }

    private void Mark(Vector3 pos, Vector3 dir, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawSphere(pos, 0.05f * Mathf.Max(1f, standingHeight));
        Gizmos.DrawRay(pos, dir * standingHeight * 0.5f);
    }

    // ---------- overlays ----------

    private void MakeOverlays()
    {
        icon = new GameObject("MoodIcon").AddComponent<SpriteRenderer>();
        icon.sortingOrder = 100;
        bubbleBack = new GameObject("SpeechBubble").AddComponent<SpriteRenderer>();
        bubbleBack.sprite = UIKit.Rounded;
        bubbleBack.drawMode = SpriteDrawMode.Sliced;
        bubbleBack.size = new Vector2(7f, 2f);
        bubbleBack.color = new Color(1f, 1f, 1f, 0.92f);
        bubbleBack.sortingOrder = 100;
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(bubbleBack.transform, false);
        bubbleText = textGo.AddComponent<TextMeshPro>();
        bubbleText.font = UIKit.BodyFont;
        bubbleText.fontSize = 1.1f;
        bubbleText.color = new Color(0.15f, 0.12f, 0.2f);
        bubbleText.alignment = TextAlignmentOptions.Center;
        bubbleText.rectTransform.sizeDelta = new Vector2(6.4f, 1.8f);
        bubbleText.sortingOrder = 101;
        bubbleBack.gameObject.SetActive(false);
        StartCoroutine(IconLoop());
    }

    private System.Collections.IEnumerator IconLoop()
    {
        while (true)
        {
            Show(icons == null ? null : icons.For(Mood));
            yield return new WaitForSeconds(0.5f);
        }
    }

    private Mood shown = (Mood)(-1);

    // Anchor sprite on the icon itself, the other pieces (bolt, drops) as children at their sheet offsets.
    private void Show(Sprite[] sprites)
    {
        if (shown == Mood) return;
        shown = Mood;
        foreach (Transform c in icon.transform) Destroy(c.gameObject);
        icon.sprite = sprites != null && sprites.Length > 0 ? sprites[0] : null;
        for (int i = 1; sprites != null && i < sprites.Length; i++)
        {
            var piece = new GameObject("Piece").AddComponent<SpriteRenderer>();
            piece.sprite = sprites[i];
            piece.sortingOrder = icon.sortingOrder;
            piece.transform.SetParent(icon.transform, false);
            piece.transform.localPosition = (sprites[i].rect.center - sprites[0].rect.center) / sprites[0].pixelsPerUnit;
        }
    }

    private void OnDestroy()
    {
        if (icon != null) Destroy(icon.gameObject);
        if (bubbleBack != null) Destroy(bubbleBack.gameObject);
    }
}
