using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>Shared IMGUI look: Inter font, dark glass panels, table helpers, portrait-aware sizes.</summary>
    public static class UiTheme
    {
        public static Font Regular, SemiBold, Bold;
        public static GUIStyle Title, H2, Body, Small, Mono, Big, Cell, CellBold, CellRight, Button;
        static bool _ready;
        public static readonly Color Panel = new Color(0.04f, 0.045f, 0.07f, 0.82f);
        public static readonly Color PanelLine = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.25f);
        public static readonly Color Good = new Color(0.35f, 0.9f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.35f, 0.3f);
        public static readonly Color Muted = new Color(0.72f, 0.74f, 0.8f);

        /// <summary>Scale factor so the HUD reads the same on 720p, 1080p and portrait 1080x1920.</summary>
        public static float S => Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f * (Screen.height > Screen.width ? 1.35f : 1.0f), 0.6f, 2.2f);
        public static bool Portrait => Screen.height > Screen.width;

        public static void Ensure()
        {
            if (_ready) return;
            Regular = Resources.Load<Font>("Fonts/Inter-Regular");
            SemiBold = Resources.Load<Font>("Fonts/Inter-SemiBold") ?? Regular;
            Bold = Resources.Load<Font>("Fonts/Inter-Bold") ?? SemiBold;
            var baseStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, normal = { textColor = Color.white } };
            if (Regular != null) baseStyle.font = Regular;
            Title = new GUIStyle(baseStyle) { font = Bold ?? baseStyle.font, fontSize = 24 };
            H2 = new GUIStyle(baseStyle) { font = SemiBold ?? baseStyle.font, fontSize = 17 };
            Body = new GUIStyle(baseStyle) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(0.92f, 0.93f, 0.95f) } };
            Small = new GUIStyle(baseStyle) { fontSize = 12, normal = { textColor = Muted } };
            Mono = new GUIStyle(baseStyle) { fontSize = 14 };
            Big = new GUIStyle(baseStyle) { font = Bold ?? baseStyle.font, fontSize = 40, alignment = TextAnchor.MiddleCenter };
            Cell = new GUIStyle(baseStyle) { fontSize = 14, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            CellBold = new GUIStyle(Cell) { font = SemiBold ?? baseStyle.font };
            CellRight = new GUIStyle(Cell) { alignment = TextAnchor.MiddleRight };
            Button = new GUIStyle(GUI.skin.button) { fontSize = 18, font = SemiBold ?? baseStyle.font, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 18, 10, 10) };
            _ready = true;
        }

        public static void Panel_(Rect r, float alpha = -1f)
        {
            var c = Panel; if (alpha >= 0f) c.a = alpha;
            GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = PanelLine; GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public static void Bar(Rect r, float frac, Color c, Color bg)
        {
            GUI.color = bg; GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public static GUIStyle Sized(GUIStyle s, float scale)
        {
            var c = new GUIStyle(s); c.fontSize = Mathf.RoundToInt(s.fontSize * scale); return c;
        }

        public static string Color_(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGBA(c)}>{s}</color>";
    }
}
