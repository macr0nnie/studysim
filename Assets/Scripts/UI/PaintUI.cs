using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Paint drawer, the game's one colour system: walls and floor (colours and materials), the selected piece
// (select it in edit mode), the menus' theme colour and the background behind the room. Every target
// has a reset. Pieces that can't be recoloured say so.
public class PaintUI : MonoBehaviour
{
    private enum Target { Walls, Floor, Piece, Menus, Background }
    private const string SaveKey = "RoomPaint";

    [Serializable]
    private class PaintSave
    {
        public string wallColor = "", floorColor = "";
        public int wallMaterial = -1, floorMaterial = -1;
        public string backgroundColor = "";
    }

    // Soft, room-friendly paints.
    static readonly string[] Swatches =
    {
        "F4EDE1", "E8D9C4", "D9C2A6", "B89A7A", "8C6A55", "5E4636",
        "F2D4D7", "D9A5B3", "B9A3D6", "8E7CC3", "5D4E8C", "3B3355",
        "CFE3D4", "9DC3A5", "6E9C7D", "A9C8E0", "6F95B8", "2F3E52",
    };

    private RoomManager room;
    private RoomSurfaces surfaces;
    private PaintSave paint = new PaintSave();
    private GameObject panel, materialsLabel, materialScroll;
    private Transform swatchGrid, materialGrid;
    private TMP_Text info;
    private Button resetButton;
    private Image[] tabs;
    private Target target = Target.Walls;
    private GameObject shownPiece;
    private Camera backgroundCamera;
    private Color originalBackground;
    private CameraClearFlags originalClear;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsurePaint();
        EnsurePaint();
    }

    private static void EnsurePaint()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<PaintUI>() == null)
            new GameObject("Paint").AddComponent<PaintUI>();
    }

    private void Awake()
    {
        room = FindFirstObjectByType<RoomManager>();
        surfaces = Resources.Load<RoomSurfaces>("RoomSurfaces");
        BuildUI();
    }

    // After RoomManager.Start has found the wall and floor meshes.
    private void Start()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (json.Length > 0)
        {
            try { paint = JsonUtility.FromJson<PaintSave>(json) ?? new PaintSave(); }
            catch (ArgumentException) { paint = new PaintSave(); } // corrupted: start unpainted
        }
        ApplySurface(Target.Walls);
        ApplySurface(Target.Floor);
        backgroundCamera = Camera.main;
        if (backgroundCamera != null)
        {
            originalBackground = backgroundCamera.backgroundColor;
            originalClear = backgroundCamera.clearFlags;
        }
        ApplyBackground();
    }

    private void ApplyBackground()
    {
        if (backgroundCamera == null) return;
        if (paint.backgroundColor.Length > 0 && ColorUtility.TryParseHtmlString("#" + paint.backgroundColor, out Color c))
        {
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor; // a skybox would hide the colour
            backgroundCamera.backgroundColor = c;
        }
        else
        {
            backgroundCamera.clearFlags = originalClear;
            backgroundCamera.backgroundColor = originalBackground;
        }
    }

    private void Update()
    {
        // Selecting another piece in edit mode while the Piece tab is open updates it.
        if (panel.activeSelf && target == Target.Piece && room.SelectedPiece != shownPiece) Refresh();
    }

    public void Toggle()
    {
        if (ToggleDrawer(this, panel)) Refresh();
    }

    private void ShowTarget(Target t)
    {
        target = t;
        Refresh();
    }

    private Renderer[] Renderers(Target t) => t == Target.Walls ? room.WallRenderers : room.FloorRenderers;
    private Material[] Materials(Target t) => surfaces == null ? new Material[0] : t == Target.Walls ? surfaces.walls : surfaces.floors;

    private void Refresh()
    {
        for (int i = 0; i < tabs.Length; i++) tabs[i].color = i == (int)target ? SelectedColor : TabColor;
        shownPiece = room.SelectedPiece;

        bool canPaint;
        bool painted;
        if (target == Target.Menus)
        {
            canPaint = true;
            painted = MenuColorChanged;
            info.text = "The colour of every menu and panel.";
        }
        else if (target == Target.Background)
        {
            canPaint = backgroundCamera != null;
            painted = paint.backgroundColor.Length > 0;
            info.text = "The colour behind the room.";
        }
        else if (target == Target.Piece)
        {
            canPaint = room.CanPaint(shownPiece);
            painted = room.IsPainted(shownPiece);
            info.text = shownPiece == null ? "Select a piece in edit mode (Esc) to paint it."
                : canPaint ? $"Painting {shownPiece.name}."
                : $"{shownPiece.name} can't be recoloured: its look comes from its texture.";
        }
        else
        {
            Renderer[] renderers = Renderers(target);
            canPaint = renderers.Length > 0 && RoomManager.Paintable(renderers);
            painted = target == Target.Walls ? paint.wallColor.Length > 0 || paint.wallMaterial >= 0
                : paint.floorColor.Length > 0 || paint.floorMaterial >= 0;
            info.text = renderers.Length == 0 ? $"No {target.ToString().ToLowerInvariant()} found in this room."
                : canPaint ? "Pick a colour or a material."
                : "This surface is textured, so it can't be recoloured. Pick a material instead.";
        }

        foreach (Button swatch in swatchGrid.GetComponentsInChildren<Button>(true)) swatch.interactable = canPaint;
        resetButton.interactable = painted;

        bool surface = target == Target.Walls || target == Target.Floor;
        materialsLabel.SetActive(surface && Materials(target).Length > 0);
        materialScroll.SetActive(surface && Materials(target).Length > 0);
        for (int i = materialGrid.childCount - 1; i >= 0; i--) Destroy(materialGrid.GetChild(i).gameObject);
        if (surface)
        {
            Material[] materials = Materials(target);
            for (int i = 0; i < materials.Length; i++) AddMaterialTile(materials[i], i);
        }
    }

    private void Pick(Color color)
    {
        if (target == Target.Menus) UIKit.ApplyMenuColor(color);
        else if (target == Target.Background)
        {
            paint.backgroundColor = ColorUtility.ToHtmlStringRGB(color);
            ApplyBackground();
            Save();
        }
        else if (target == Target.Piece)
        {
            room.PaintPiece(shownPiece, color);
        }
        else
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            if (target == Target.Walls) paint.wallColor = hex; else paint.floorColor = hex;
            ApplySurface(target);
            Save();
        }
        Refresh();
    }

    private void PickMaterial(int index)
    {
        if (target == Target.Walls) { paint.wallMaterial = index; paint.wallColor = ""; }
        else { paint.floorMaterial = index; paint.floorColor = ""; }
        ApplySurface(target);
        Save();
        Refresh();
    }

    private void ResetTarget()
    {
        if (target == Target.Menus) UIKit.ApplyMenuColor(null);
        else if (target == Target.Background)
        {
            paint.backgroundColor = "";
            ApplyBackground();
            Save();
        }
        else if (target == Target.Piece) room.PaintPiece(shownPiece, null);
        else
        {
            if (target == Target.Walls) { paint.wallColor = ""; paint.wallMaterial = -1; }
            else { paint.floorColor = ""; paint.floorMaterial = -1; }
            ApplySurface(target);
            Save();
        }
        Refresh();
    }

    // Original materials, then the chosen material, then the chosen colour on top.
    private void ApplySurface(Target t)
    {
        Renderer[] renderers = Renderers(t);
        room.Tint(renderers, null);
        int index = t == Target.Walls ? paint.wallMaterial : paint.floorMaterial;
        Material[] materials = Materials(t);
        if (index >= 0 && index < materials.Length) room.SetSurfaceMaterial(renderers, materials[index]);
        string hex = t == Target.Walls ? paint.wallColor : paint.floorColor;
        if (hex.Length > 0 && ColorUtility.TryParseHtmlString("#" + hex, out Color c)) room.Tint(renderers, c);
    }

    private void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(paint));
        PlayerPrefs.Save();
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("PaintCanvas", transform, 10).transform;
        Button open = NavButton(canvas, 2, "Paint", null);
        open.onClick.AddListener(Toggle);

        panel = MakeDrawer("PaintPanel", canvas);
        Header(panel.transform, "Paint", Toggle);

        GameObject tabRow = Row("Tabs", panel.transform, 34, 6, true);
        tabs = new Image[Enum.GetValues(typeof(Target)).Length];
        foreach (Target t in Enum.GetValues(typeof(Target)))
        {
            Target captured = t;
            Button tab = TextButton(t.ToString(), tabRow.transform, t.ToString(), TabColor, CaptionSize);
            tab.onClick.AddListener(() => ShowTarget(captured));
            tabs[(int)t] = tab.GetComponent<Image>();
        }

        info = MakeText("Info", panel.transform, "", LabelSize, MutedText, TextAlignmentOptions.Left);
        info.textWrappingMode = TextWrappingModes.Normal;
        info.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

        Label("Colours");
        swatchGrid = Grid(Make("Swatches", panel.transform), 38, 9, 6).transform;
        foreach (string hex in Swatches)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            GameObject swatch = Make(hex, swatchGrid, typeof(Image), typeof(Button));
            Image image = swatch.GetComponent<Image>();
            image.sprite = Circle;
            image.color = color;
            swatch.GetComponent<Button>().onClick.AddListener(() => Pick(color));
        }

        materialsLabel = Label("Materials");
        materialGrid = Grid(ScrollList(panel.transform, out ScrollRect scroll).gameObject, 56, 6, 8).transform;
        materialScroll = scroll.gameObject;

        GameObject spacer = Make("Spacer", panel.transform, typeof(LayoutElement));
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
        resetButton = TextButton("Reset", panel.transform, "Reset colour", TabColor, BodySize);
        resetButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        resetButton.onClick.AddListener(ResetTarget);
    }

    private GameObject Label(string text)
    {
        TMP_Text label = MakeText(text + "Label", panel.transform, text, BodySize, TextColor, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        return label.gameObject;
    }

    // The drawer's layout sizes a grid by its preferred height; a scroll list's fitter does the same.
    private static GameObject Grid(GameObject grid, float cell, int columns, float spacing)
    {
        var layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(cell, cell);
        layout.spacing = new Vector2(spacing, spacing);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        return grid;
    }

    // A tile showing the material's colour (and texture, if it has one).
    private void AddMaterialTile(Material material, int index)
    {
        GameObject tile = Make(material.name, materialGrid, typeof(Image), typeof(Button), typeof(Mask));
        Style(tile.GetComponent<Image>(), Color.white);
        tile.GetComponent<Mask>().showMaskGraphic = true;
        tile.GetComponent<Button>().onClick.AddListener(() => PickMaterial(index));
        GameObject swatch = Make("Swatch", tile.transform, typeof(RawImage));
        Anchor(swatch, Vector2.zero, Vector2.one);
        var raw = swatch.GetComponent<RawImage>();
        raw.texture = material.mainTexture;
        raw.color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        raw.raycastTarget = false;
        int chosen = target == Target.Walls ? paint.wallMaterial : paint.floorMaterial;
        if (chosen == index) tile.GetComponent<Image>().color = AccentColor; // ring on the chosen one
        ((RectTransform)swatch.transform).offsetMin = new Vector2(4, 4);
        ((RectTransform)swatch.transform).offsetMax = new Vector2(-4, -4);
    }
}
