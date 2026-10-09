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
    private Vector3 usedPos;

    private void Start()
    {
        room = FindFirstObjectByType<RoomManager>();
        timer = FindFirstObjectByType<TimerManager>();
        animator = GetComponent<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        icons = Resources.Load<MoodIcons>("MoodIcons");
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

    private void UseFurniture()
    {
        if (room == null || animator == null) return;
        bool wantLie = timer != null && !timer.IsStudySession;
        GameObject best = null;
        IReadOnlyList<GameObject> pieces = room.PlacedPieces;
        for (int i = pieces.Count - 1; i >= 0 && best == null; i--) // newest first: a bought chair beats the desk
        {
            GameObject p = pieces[i];
            if (p == null || !p.activeInHierarchy) continue;
            FurnitureItem item = room.ItemOf(p);
            if (item != null && (wantLie ? item.lie : item.sit).enabled) best = p;
        }
        if (best == null) { if (usedPiece != null) { usedPiece = null; Release(); } return; }
        if (best == usedPiece && wantLie == lying) return;
        usedPiece = best;
        lying = wantLie;
        Interaction spot = (lying ? room.ItemOf(best).lie : room.ItemOf(best).sit);
        usedPos = spot.localPosition;
        yaw = spot.yaw;
        if (lying) { animator.SetBool("IsStudying", false); animator.speed = 0f; } // no lying clip yet: freeze the pose
        else { animator.speed = 1f; animator.SetBool("IsStudying", true); }
    }

    private float yaw;

    // Follows the piece (and ignores the shake while the room falls apart).
    private void Hold()
    {
        Distraction.RestPose(usedPiece.transform, out Vector3 p, out Quaternion r);
        transform.SetPositionAndRotation(p + r * Vector3.Scale(usedPos, usedPiece.transform.lossyScale),
            r * Quaternion.Euler(lying ? -90f : 0f, yaw, 0f));
    }

    private void Release()
    {
        lying = false;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (animator != null) { animator.speed = 1f; animator.SetBool("IsStudying", true); }
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
            icon.sprite = icons == null ? null : icons.For(Mood);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnDestroy()
    {
        if (icon != null) Destroy(icon.gameObject);
        if (bubbleBack != null) Destroy(bubbleBack.gameObject);
    }
}
