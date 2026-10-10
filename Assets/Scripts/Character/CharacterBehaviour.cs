using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Everything the character does besides animate: uses furniture (sit when studying, lie on a break), keeps a hidden
// happiness index, shows a Sims-style bubble over its head: the mood icon, plus a short line when it speaks.
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
    private Canvas overlay;
    private RectTransform head, bubble, tail;
    private Image icon;
    private TMP_Text bubbleText;
    private string line;
    private float pop = 1f;
    private MoodIcons icons;
    private float nextSay, hideBubble, nextCheck;
    private GameObject usedPiece;
    private bool lying;

    private void Start()
    {
        room = FindAnyObjectByType<RoomManager>();
        timer = FindAnyObjectByType<TimerManager>();
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

        if (Time.time >= nextCheck) { nextCheck = Time.time + 0.25f; UseFurniture(); }
        if (Time.time >= nextSay && !lying) { Say(CharacterDialogue.Line(Mood, CharacterDialogue.Stage)); nextSay = Time.time + SayEvery * Random.Range(0.8f, 1.6f); }
        if (line != null && Time.time >= hideBubble) { line = null; Layout(); }
        if (walking) Walk();
        else if (usedPiece != null) Hold();
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || icon == null) return;
        Bounds b = default; bool found = false;
        foreach (Renderer r in renderers) if (r != null && r.enabled && !(r is SpriteRenderer)) { if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds); }
        if (!found) return;
        // Screen space, so the bubble stays the same readable size at any zoom; it sits just above the head.
        Vector3 p = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z));
        head.gameObject.SetActive(p.z > 0 && (icon.sprite != null || line != null));
        head.position = new Vector3(p.x, p.y + 12f * overlay.scaleFactor, 0f);
        pop = Mathf.MoveTowards(pop, 1f, Time.unscaledDeltaTime * 5f);
        float t = pop - 1f;
        head.localScale = Vector3.one * (1f + 2.7f * t * t * t + 1.7f * t * t); // ease-out-back: pops in with a small overshoot
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
        this.line = line;
        hideBubble = Time.time + SayFor;
        Layout();
        pop = 0f;
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
        // The chair's real front (backrest side of the mesh, or the item's Front yaw), so the placed chair and the sitter agree.
        FurnitureItem item = room.ItemOf(chair.gameObject);
        // Reading the mesh is slow, so the front is worked out once per chair and kept in the chair's own frame.
        if (!localFronts.TryGetValue(chair, out Vector3 localFront))
            localFronts[chair] = localFront = Quaternion.Inverse(chair.rotation) * RoomManager.ChairFront(chair.gameObject, item != null ? item.frontYaw : 0f);
        Distraction.RestPose(chair, out _, out Quaternion chairRot);
        Vector3 face = Vector3.ProjectOnPlane(chairRot * localFront, Vector3.up);
        if (face.sqrMagnitude < 0.0001f) face = toDesk.sqrMagnitude > 0f ? toDesk : Vector3.back;
        rot = Quaternion.AngleAxis(item != null ? item.sit.yaw : 0f, Vector3.up) * Quaternion.LookRotation(face.normalized); // sit.yaw tunes a chair whose front is off
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
        // Nothing to lie on (a new game has only a desk and chair): stay seated at the desk instead of standing where it
        // spawned, which faces the camera.
        if (best == null) wantLie = false;
        if (!wantLie && Chairs(out List<Transform> chairs))
        {
            // Several chairs: one with a desk within reach beats one without, then the most recently placed or moved,
            // then the closest to a desk.
            bool bestNear = false; float bestStamp = -1f;
            foreach (Transform c in chairs)
            {
                Distraction.RestPose(c, out Vector3 cp, out Quaternion cr);
                if (!stamps.TryGetValue(c, out Stamp s) || (s.pos - cp).sqrMagnitude > 0.0001f || Quaternion.Angle(s.rot, cr) > 1f)
                    stamps[c] = s = new Stamp { pos = cp, rot = cr, time = Time.time };
                if (!SitSpot(c, out Vector3 p2, out Quaternion r2, out float dist)) continue;
                bool near = dist <= standingHeight * 0.6f;
                bool better = best == null || (near != bestNear ? near : s.time != bestStamp ? s.time > bestStamp : dist < bestDist);
                if (!better) continue;
                best = c.gameObject; pos = p2; rot = r2; bestDist = dist; bestNear = near; bestStamp = s.time;
            }
        }
        if (best == null) { if (usedPiece != null || walking) { usedPiece = null; walking = false; Release(); } return; }
        if (!wantLie && best != usedPiece)
        {
            // A different chair: walk there instead of snapping (but not while a piece is being dragged).
            if (room.IsDragging) return;
            if (walking && best == walkChair) { sitPos = pos; sitRot = rot; return; }
            BeginWalk(best, pos, rot);
            return;
        }
        if (walking && wantLie) { walking = false; Release(); } // break time: stop walking, go lie down
        if (walking) { sitPos = pos; sitRot = rot; return; }
        Assign(best, wantLie, pos, rot);
    }

    private void Assign(GameObject piece, bool lie, Vector3 pos, Quaternion rot)
    {
        if (usedPiece == null || lie != lying)
        {
            if (lie) { animator.SetBool("IsStudying", false); animator.speed = 0f; } // no lying clip yet: freeze the pose
            else { animator.speed = 1f; animator.SetBool("IsStudying", true); }
        }
        usedPiece = piece;
        lying = lie;
        // Remembered in the piece's own frame, so the character rides with it instead of drifting.
        Matrix4x4 rest = Rest(piece.transform);
        localPos = rest.inverse.MultiplyPoint3x4(pos);
        localRot = Quaternion.Inverse(rest.rotation) * rot;
    }

    // ---------- walking to a chair ----------

    private const float WalkSpeed = 1.5f, MaxWalkSeconds = 8f, TurnDegreesPerSecond = 360f;
    private bool walking;
    private GameObject walkChair;
    private Vector3 sitPos;
    private Quaternion sitRot;
    private readonly List<Vector3> path = new List<Vector3>();
    private float walkTime;

    // Stand up, go round the furniture to a free spot beside the chair, then step onto it. No NavMesh: a straight line
    // with a sidestep round any blocking piece's bounds is enough for a bedroom.
    private void BeginWalk(GameObject chair, Vector3 pos, Quaternion rot)
    {
        Vector3 start = transform.position;
        GameObject oldPiece = usedPiece;
        bool wasSeated = oldPiece != null && !lying;
        usedPiece = null; lying = false;
        walking = true; walkChair = chair; sitPos = pos; sitRot = rot; walkTime = 0f;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        animator.SetBool("IsStudying", false);
        animator.speed = 0f; // shortcut: the controller has no walk clip, so the pose just glides; add one and set a Walk bool here

        float step = standingHeight * 0.35f;
        Vector3 face = rot * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, face);
        Vector3[] candidates = { pos + face * step, pos + side * step, pos - side * step, pos - face * step };
        System.Array.Sort(candidates, 1, 2, Comparer<Vector3>.Create((a, b) => (a - start).sqrMagnitude.CompareTo((b - start).sqrMagnitude))); // the nearer side first
        var ignore = new HashSet<GameObject> { chair, oldPiece };
        Vector3 stand = candidates[0];
        foreach (Vector3 c in candidates) if (!Blocked(c, ignore)) { stand = c; break; }

        path.Clear();
        Vector3 from = start;
        // Out of a seat first: back away from the desk instead of cutting through it.
        if (wasSeated) { from = start - transform.forward * standingHeight * 0.3f; path.Add(from); }
        path.AddRange(Detour(from, stand, ignore));
        path.Add(stand);
        path.Add(pos);
    }

    private Bounds Inflated(GameObject p, float r)
    {
        Bounds b = BoundsOf(p);
        b.Expand(new Vector3(r * 2f, 0f, r * 2f));
        return b;
    }

    private bool Blocked(Vector3 point, HashSet<GameObject> ignore)
    {
        float r = standingHeight * 0.12f, floorY = FloorBounds().max.y;
        foreach (GameObject p in room.PlacedPieces)
        {
            if (p == null || !p.activeInHierarchy || ignore.Contains(p)) continue;
            Bounds b = Inflated(p, r);
            if (b.max.y - floorY < standingHeight * 0.15f) continue; // rugs and the like are walked over
            if (point.x > b.min.x && point.x < b.max.x && point.z > b.min.z && point.z < b.max.z) return true;
        }
        return false;
    }

    // Waypoints round whatever blocks the straight line from a to b (pieces that already contain a or b don't count).
    private List<Vector3> Detour(Vector3 a, Vector3 b, HashSet<GameObject> ignore)
    {
        var points = new List<Vector3>();
        float r = standingHeight * 0.12f, floorY = FloorBounds().max.y;
        Vector3 cur = a;
        for (int pass = 0; pass < 4; pass++)
        {
            Bounds hit = default; bool found = false;
            foreach (GameObject p in room.PlacedPieces)
            {
                if (p == null || !p.activeInHierarchy || ignore.Contains(p)) continue;
                Bounds o = Inflated(p, r);
                if (o.max.y - floorY < standingHeight * 0.15f) continue;
                if (Inside(o, cur) || Inside(o, b) || !Crosses(o, cur, b)) continue;
                hit = o; found = true; break;
            }
            if (!found) break;
            Vector3 best = default; float bestLen = float.MaxValue;
            foreach (Vector3 c in new[] { new Vector3(hit.min.x, cur.y, hit.min.z), new Vector3(hit.min.x, cur.y, hit.max.z), new Vector3(hit.max.x, cur.y, hit.min.z), new Vector3(hit.max.x, cur.y, hit.max.z) })
            {
                float len = (c - cur).magnitude + (b - c).magnitude;
                if (len < bestLen) { bestLen = len; best = c; }
            }
            points.Add(best);
            cur = best;
        }
        return points;
    }

    private static bool Inside(Bounds o, Vector3 p) => p.x > o.min.x && p.x < o.max.x && p.z > o.min.z && p.z < o.max.z;

    // Does the segment a-b cross the box on the floor plane (slab test)?
    private static bool Crosses(Bounds o, Vector3 a, Vector3 b)
    {
        float t0 = 0f, t1 = 1f;
        float[] da = { b.x - a.x, b.z - a.z }, lo = { o.min.x - a.x, o.min.z - a.z }, hi = { o.max.x - a.x, o.max.z - a.z };
        for (int i = 0; i < 2; i++)
        {
            if (Mathf.Abs(da[i]) < 1e-6f) { if (lo[i] > 0f || hi[i] < 0f) return false; continue; }
            float u = lo[i] / da[i], v = hi[i] / da[i];
            if (u > v) { float s = u; u = v; v = s; }
            t0 = Mathf.Max(t0, u); t1 = Mathf.Min(t1, v);
            if (t0 > t1) return false;
        }
        return true;
    }

    private void Walk()
    {
        if (room != null && room.IsDragging) return; // hold still while the player drags furniture around
        if (walkChair == null || !walkChair.activeInHierarchy) { walking = false; Release(); return; } // chair gone: the next check picks another
        walkTime += Time.deltaTime;
        if (walkTime > MaxWalkSeconds) { Finish(); return; } // blocked or too slow: just sit

        if (path.Count > 0)
        {
            Vector3 target = path[0], to = target - transform.position;
            to.y = 0f;
            float step = WalkSpeed * Time.deltaTime;
            if (to.magnitude <= step) { transform.position = new Vector3(target.x, target.y, target.z); path.RemoveAt(0); }
            else
            {
                transform.position += to.normalized * step;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized), TurnDegreesPerSecond * Time.deltaTime);
            }
            return;
        }
        // At the seat: turn to match the chair, then sit.
        transform.position = sitPos;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, sitRot, TurnDegreesPerSecond * Time.deltaTime);
        if (Quaternion.Angle(transform.rotation, sitRot) < 2f) Finish();
    }

    private void Finish()
    {
        walking = false;
        Assign(walkChair, false, sitPos, sitRot);
    }

    private readonly Dictionary<Transform, Vector3> localFronts = new Dictionary<Transform, Vector3>();
    private struct Stamp { public Vector3 pos; public Quaternion rot; public float time; }
    private readonly Dictionary<Transform, Stamp> stamps = new Dictionary<Transform, Stamp>();
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

    private const float IconSize = 64f, Pad = 14f, MaxTextWidth = 340f;

    private void MakeOverlays()
    {
        overlay = UIKit.MakeCanvas("CharacterBubble", null, -5); // under the HUD's panels
        Destroy(overlay.GetComponent<GraphicRaycaster>()); // never blocks clicks on the room
        head = (RectTransform)UIKit.Make("Head", overlay.transform).transform;
        head.anchorMin = head.anchorMax = Vector2.zero;
        head.sizeDelta = Vector2.zero;
        bubble = (RectTransform)UIKit.Make("Bubble", head, typeof(Image)).transform;
        bubble.pivot = new Vector2(0.5f, 0f);
        bubble.anchoredPosition = new Vector2(0f, 14f);
        UIKit.Style(bubble.GetComponent<Image>(), Color.white);
        bubble.GetComponent<Image>().raycastTarget = false;
        // UIKit.Triangle points right; turned to point down at the head.
        tail = (RectTransform)UIKit.Make("Tail", head, typeof(Image)).transform;
        tail.sizeDelta = new Vector2(24f, 22f);
        tail.anchoredPosition = new Vector2(0f, 8f);
        tail.localRotation = Quaternion.Euler(0f, 0f, -90f);
        var tailImage = tail.GetComponent<Image>();
        tailImage.sprite = UIKit.Triangle;
        tailImage.raycastTarget = false;
        icon = UIKit.Make("MoodIcon", bubble, typeof(Image)).GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        bubbleText = UIKit.MakeText("Line", bubble, "", 30f, new Color(0.15f, 0.12f, 0.2f), TextAlignmentOptions.Left);
        bubbleText.enableAutoSizing = false;
        bubbleText.overflowMode = TextOverflowModes.Overflow;
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        Layout();
        StartCoroutine(IconLoop());
    }

    // A round bubble with just the icon; when speaking it widens to fit the line (wrapped at MaxTextWidth) beside the icon.
    private void Layout()
    {
        bool hasIcon = icon.sprite != null, talking = line != null;
        bubbleText.gameObject.SetActive(talking);
        float textW = 0f, textH = 0f;
        if (talking)
        {
            bubbleText.text = line;
            textW = Mathf.Min(bubbleText.GetPreferredValues(line).x, MaxTextWidth);
            textH = bubbleText.GetPreferredValues(line, textW, 0f).y;
        }
        float iconW = hasIcon ? IconSize : 0f, gap = hasIcon && talking ? 10f : 0f;
        float h = Mathf.Max(iconW, textH) + Pad * 2f;
        float w = Mathf.Max(h, iconW + gap + textW + Pad * 2f);
        bubble.sizeDelta = new Vector2(w, h);
        Place((RectTransform)icon.transform, Pad + (talking ? 0f : (w - Pad * 2f - iconW) / 2f), new Vector2(iconW, iconW), h);
        Place((RectTransform)bubbleText.transform, Pad + iconW + gap, new Vector2(textW, textH), h);
    }

    // Positions a child of the bubble x pixels from its left edge, vertically centred.
    private static void Place(RectTransform r, float x, Vector2 size, float bubbleH)
    {
        r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
        r.sizeDelta = size;
        r.anchoredPosition = new Vector2(x, (bubbleH - size.y) / 2f);
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
        Sprite anchor = sprites != null && sprites.Length > 0 ? sprites[0] : null;
        icon.sprite = anchor;
        Layout();
        pop = 0f;
        if (anchor == null) return;
        float scale = IconSize / Mathf.Max(anchor.rect.width, anchor.rect.height); // sheet pixels -> bubble pixels
        for (int i = 1; i < sprites.Length; i++)
        {
            var piece = UIKit.Make("Piece", icon.transform, typeof(Image)).GetComponent<Image>();
            piece.sprite = sprites[i];
            piece.raycastTarget = false;
            var r = (RectTransform)piece.transform;
            r.sizeDelta = sprites[i].rect.size * scale;
            r.anchoredPosition = (sprites[i].rect.center - anchor.rect.center) * scale;
        }
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
