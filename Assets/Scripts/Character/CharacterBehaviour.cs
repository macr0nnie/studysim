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
    private static bool Named(GameObject g, string part) => g.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;

    private bool Usable(GameObject p, bool lie, out Interaction data)
    {
        FurnitureItem item = room.ItemOf(p);
        data = item == null ? default : lie ? item.lie : item.sit;
        if (data.enabled) return true;
        if (lie ? Named(p, "bed") && !Named(p, "plane") : Named(p, "chair") || Named(p, "desk")) { data.enabled = true; data.height = lie ? 0.6f : 0f; return true; }
        return false;
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

    // World position and rotation for the character on this piece: from its bounds, never from its local axes.
    private bool Spot(GameObject piece, bool lie, Interaction d, out Vector3 pos, out Quaternion rot)
    {
        pos = default; rot = Quaternion.identity;
        Transform chair = lie ? null : Child(piece, "chair");
        Bounds b = BoundsOf(chair != null ? chair.gameObject : piece);
        if (b.size == Vector3.zero) return false;
        pos = new Vector3(b.center.x + d.offset.x * b.extents.x, b.min.y + d.height * b.size.y, b.center.z + d.offset.y * b.extents.z);
        Camera cam = Camera.main;
        Vector3 toCam = cam ? Vector3.ProjectOnPlane(cam.transform.position - b.center, Vector3.up).normalized : Vector3.back;
        if (lie)
        {
            // Head toward the nearer wall along the bed's long side; the pivot (the feet) is half a body back from the middle.
            Bounds floor = FloorBounds();
            bool alongX = b.size.x >= b.size.z;
            float c = alongX ? b.center.x : b.center.z, lo = alongX ? floor.min.x : floor.min.z, hi = alongX ? floor.max.x : floor.max.z;
            float sign = floor.size == Vector3.zero || Mathf.Abs(c - lo) < Mathf.Abs(hi - c) ? -1f : 1f;
            Vector3 head = (alongX ? Vector3.right : Vector3.forward) * sign;
            pos -= head * standingHeight * 0.5f;
            rot = Quaternion.AngleAxis(d.yaw, Vector3.up) * Quaternion.LookRotation(Vector3.up, head);
            return true;
        }
        // Facing the nearest desk; a desk with no chair of its own gets the player on its camera side.
        GameObject desk = null; float best = float.MaxValue;
        foreach (GameObject p in room.PlacedPieces)
        {
            if (p == null || !p.activeInHierarchy || !Named(p, "desk")) continue;
            float dist = (BoundsOf(p).center - b.center).sqrMagnitude;
            if (dist < best) { best = dist; desk = p; }
        }
        if (desk == piece && chair == null) pos += toCam * Mathf.Max(b.extents.x, b.extents.z);
        Vector3 face = desk == null ? -toCam : Vector3.ProjectOnPlane(BoundsOf(desk).center - pos, Vector3.up);
        if (face.sqrMagnitude < 0.0001f) face = -toCam;
        rot = Quaternion.AngleAxis(d.yaw, Vector3.up) * Quaternion.LookRotation(face.normalized);
        return true;
    }

    private void UseFurniture()
    {
        if (room == null || animator == null) return;
        bool wantLie = timer != null && !timer.IsStudySession;
        GameObject best = null; int bestScore = 0; Interaction bestData = default;
        IReadOnlyList<GameObject> pieces = room.PlacedPieces;
        for (int i = pieces.Count - 1; i >= 0; i--) // newest first, and a chair beats a desk
        {
            GameObject p = pieces[i];
            if (p == null || !p.activeInHierarchy || !Usable(p, wantLie, out Interaction d)) continue;
            int score = wantLie || !Named(p, "desk") ? 2 : 1;
            if (score > bestScore) { best = p; bestScore = score; bestData = d; }
        }
        if (best == null) { if (usedPiece != null) { usedPiece = null; Release(); } return; }
        if (best == usedPiece && wantLie == lying) return;
        if (!Spot(best, wantLie, bestData, out Vector3 pos, out Quaternion rot)) return;
        usedPiece = best;
        lying = wantLie;
        // Remembered in the piece's own frame, so the character rides with it instead of drifting.
        Matrix4x4 rest = Rest(best.transform);
        localPos = rest.inverse.MultiplyPoint3x4(pos);
        localRot = Quaternion.Inverse(rest.rotation) * rot;
        if (lying) { animator.SetBool("IsStudying", false); animator.speed = 0f; } // no lying clip yet: freeze the pose
        else { animator.speed = 1f; animator.SetBool("IsStudying", true); }
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

    // Debug: blue = where the character would sit, pink = where it would lie, for every piece that can be used.
    private void OnDrawGizmos()
    {
        if (room == null) return;
        foreach (GameObject p in room.PlacedPieces)
            foreach (bool lie in new[] { false, true })
                if (p != null && p.activeInHierarchy && Usable(p, lie, out Interaction d) && Spot(p, lie, d, out Vector3 pos, out Quaternion rot))
                {
                    Gizmos.color = lie ? Color.magenta : Color.cyan;
                    Gizmos.DrawSphere(pos, 0.05f * Mathf.Max(1f, standingHeight));
                    Gizmos.DrawRay(pos, rot * (lie ? Vector3.up : Vector3.forward) * standingHeight * 0.5f);
                }
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
