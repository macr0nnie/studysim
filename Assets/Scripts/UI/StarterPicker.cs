using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UIKit;

// New-game drawer: pick the starter desk, then the starter chair, from the store's desks and seating. Free, and
// the room already has Desk 1 and Chair 1 in it, so closing early keeps those.
public class StarterPicker : MonoBehaviour
{
    private RoomManager room;
    private FurnitureCatalog catalog;
    private GameObject panel;
    private TMP_Text title;
    private Transform grid;
    private bool choosingChair;

    public static void Show(RoomManager room)
    {
        var catalog = Resources.Load<FurnitureCatalog>("FurnitureCatalog");
        if (catalog == null) return;
        var picker = new GameObject("StarterPicker").AddComponent<StarterPicker>();
        picker.room = room;
        picker.catalog = catalog;
    }

    private void Start()
    {
        Transform canvas = MakeCanvas("StarterPickerCanvas", transform, 12).transform;
        panel = MakeDrawer("StarterPanel", canvas);
        title = Header(panel.transform, "", () => Destroy(gameObject));
        MakeText("Hint", panel.transform, "Free to start with. You can swap them later in the shop and Edit mode.", LabelSize, MutedText, TextAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
        grid = ScrollList(panel.transform, out _);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(120, 140);
        layout.spacing = new Vector2(10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        Button next = TextButton("Next", panel.transform, "Next", AccentButtonColor, BodySize);
        next.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
        next.onClick.AddListener(Next);
        panel.SetActive(true);
        Fill();
    }

    private void Next()
    {
        if (choosingChair) { Destroy(gameObject); return; }
        choosingChair = true;
        Fill();
    }

    private void Fill()
    {
        title.text = choosingChair ? "Choose your chair" : "Choose your desk";
        foreach (Transform card in grid) Destroy(card.gameObject);
        string role = choosingChair ? "chair" : "desk";
        int n = 0;
        foreach (FurnitureItem item in catalog.items)
            if (item != null && item.prefab != null && FurnitureRole.Is(item.prefab, item, role)) AddCard(item, n++);
    }

    private void AddCard(FurnitureItem item, int index)
    {
        GameObject root = Make(item.name, grid, typeof(Image), typeof(Button));
        Style(root.GetComponent<Image>(), CardColor);
        bool chair = choosingChair;
        root.GetComponent<Button>().onClick.AddListener(() => room.SetStarter(item, !chair));
        var icon = Make("Icon", root.transform, typeof(Image)).GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;
        StartCoroutine(SetIcon(icon, item, index));
        Anchor(icon.gameObject, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.96f));
        Anchor(MakeText("Name", root.transform, item.displayName, CaptionSize, TextColor, TextAlignmentOptions.Center).gameObject,
            new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.24f));
    }

    // Rendering an icon is slow, so one per frame rather than all at once.
    private IEnumerator SetIcon(Image icon, FurnitureItem item, int frames)
    {
        for (int i = 0; i <= frames; i++) yield return null;
        if (icon == null) yield break;
        icon.sprite = item.icon != null ? item.icon : FurnitureIconRenderer.Render(item.prefab, 128);
        icon.enabled = icon.sprite != null;
    }
}
