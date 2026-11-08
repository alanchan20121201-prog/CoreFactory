// 字型：用系統字型讓繁中/簡中/英/日都能顯示（不打包字型檔，省包體）。
// 比照 3D 版 ThemeFactory 的作法，但 2D 版獨立一份以便自由調大小。
using Godot;

namespace CoreFactory.View2D
{
    public static class Fonts2D
    {
        public static Font Main;
        public static Font Small;
        public static Font Tiny;

        public static void Init()
        {
            Main = Make(16);
            Small = Make(13);
            Tiny = Make(11);
        }

        private static SystemFont Make(int size)
        {
            var f = new SystemFont();
            f.FontNames = new string[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC",
                "PingFang SC", "SimHei", "DengXian", "Arial",
            };
            return f;
        }

        /// <summary>建立一個套用主字型與描邊的 Label。</summary>
        public static Label Label(string text, int size, Color color)
        {
            var l = new Label { Text = text };
            l.AddThemeFontOverride("font", size <= 12 ? (size <= 11 ? Tiny : Small) : Main);
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            l.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
            l.AddThemeConstantOverride("outline_size", 4);
            return l;
        }

        public static StyleBoxFlat PanelStyle(Color bg, Color border)
        {
            var sb = new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = border,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
            };
            sb.SetBorderWidthAll(1);
            sb.ContentMarginLeft = 10;
            sb.ContentMarginRight = 10;
            sb.ContentMarginTop = 6;
            sb.ContentMarginBottom = 6;
            return sb;
        }
    }
}
