using System.Collections.Generic;
using UnityEngine;

namespace TomeOfMimicry
{
    public static class UiAssets
    {
        public static Texture2D Cursor;
        public static Font PixelFont;

        public static readonly Texture2D[] BookPages = new Texture2D[9];
        public static Texture2D Bookmarks;
        public static Texture2D BookIcons;
        public static Texture2D BookIndex;
        public static Texture2D DarkerPage;
        public static Texture2D IconSlot;

        public static readonly Dictionary<string, Texture2D> Sprites = new();

        public static bool HasFantasyBook => BookPages[0] != null;

        public static Texture2D Sprite(string name) => Sprites.TryGetValue(name, out var t) ? t : null;

        public static void RegisterTexture(Texture2D tex)
        {
            if (tex == null) return;
            tex.filterMode = FilterMode.Point;
            if (tex.name == "cursor-pointer") Cursor = tex;
            else if (tex.name == "Bookmarks") Bookmarks = tex;
            else if (tex.name == "BookIcons") BookIcons = tex;
            else if (tex.name == "BookIndex") BookIndex = tex;
            else if (tex.name == "DarkerPage") DarkerPage = tex;
            else if (tex.name == "IconSlot") IconSlot = tex;
            else if (tex.name.Length == 5 && tex.name.StartsWith("Page") && tex.name[4] >= '1' && tex.name[4] <= '9')
                BookPages[tex.name[4] - '1'] = tex;

            Sprites[tex.name] = tex;
        }

        public static void RegisterFont(Font font)
        {
            if (font != null && (font.name.StartsWith("CuteFantasy") ||
                                 (PixelFont == null && font.name.StartsWith("Tiny5"))))
                PixelFont = font;
        }
    }
}
