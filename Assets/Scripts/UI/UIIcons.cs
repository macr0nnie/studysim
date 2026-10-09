using System.Collections.Generic;
using UnityEngine;

// White copies of the game's icon sheets (Assets/ART/UI/*Light.png). The originals are dark line art, which a
// UI tint can't lighten, so dark-themed panels swap to these and tint them instead.
public class UIIcons : ScriptableObject
{
    public Sprite[] light;

    private static Dictionary<string, Sprite> byName;

    private static Dictionary<string, Sprite> All()
    {
        if (byName != null) return byName;
        byName = new Dictionary<string, Sprite>();
        var icons = Resources.Load<UIIcons>("UIIcons");
        if (icons != null)
            foreach (Sprite s in icons.light)
                if (s != null) byName[s.name] = s;
        return byName;
    }

    // The white version of an icon sprite, or null if it has none.
    public static Sprite Light(Sprite sprite)
    {
        if (sprite == null) return null;
        return All().TryGetValue(sprite.name, out Sprite white) || All().TryGetValue(sprite.name + " Light", out white) ? white : null;
    }

    // A white icon by its sheet name, e.g. "Icons_21" (coin).
    public static Sprite Named(string name) => All().TryGetValue(name, out Sprite s) ? s : null;
}
