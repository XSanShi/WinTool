using System.Windows.Media;

namespace WinTool.App.Views;

/// <summary>SF Symbols 风格的 24x24 图标几何：全部用 stroke 绘制，Fill 置 Transparent。</summary>
internal static class Icons
{
    public static Geometry Todo => Geometry.Parse("M12 21a9 9 0 1 1 0-18 9 9 0 1 1 0 18z M8 12.4l2.8 2.8 5.2-5.6");

    public static Geometry Key => Geometry.Parse("M8 12a4 4 0 1 1 0-8 4 4 0 1 1 0 8z M11.5 11.5L20 20 M15.8 15.8l2.4-2.4 M18 18l2.4-2.4");

    public static Geometry Note => Geometry.Parse("M6 3h8l4 4v14H6z M14 3v4h4 M9 12h6 M9 15.5h6 M9 8.5h3");

    public static Geometry Settings => Geometry.Parse("M12 15.5a3.5 3.5 0 1 1 0-7 3.5 3.5 0 1 1 0 7z M12 2.6v2.8 M12 18.6v2.8 M2.6 12h2.8 M18.6 12h2.8 M5.3 5.3l2 2 M16.7 16.7l2 2 M18.7 5.3l-2 2 M7.3 16.7l-2 2");

    public static Geometry Plus => Geometry.Parse("M12 5v14 M5 12h14");

    public static Geometry Eye => Geometry.Parse("M2.5 12s3.5-6.5 9.5-6.5S21.5 12 21.5 12s-3.5 6.5-9.5 6.5S2.5 12 2.5 12z M12 15a3 3 0 1 1 0-6 3 3 0 1 1 0 6z");

    public static Geometry EyeOff => Geometry.Parse("M2.5 12s3.5-6.5 9.5-6.5S21.5 12 21.5 12s-3.5 6.5-9.5 6.5S2.5 12 2.5 12z M12 15a3 3 0 1 1 0-6 3 3 0 1 1 0 6z M4 4l16 16");

    public static Geometry Copy => Geometry.Parse("M9 9h11v11H9z M5 15V5h10");

    public static Geometry Minus => Geometry.Parse("M6 12h12");

    public static Geometry ChevronRight => Geometry.Parse("M9.5 5.5L16 12l-6.5 6.5");

    public static Geometry Check => Geometry.Parse("M5 12.5l4.5 4.5L19 7");

    public static Geometry Close => Geometry.Parse("M6 6l12 12 M18 6L6 18");

    public static Geometry ChevronDown => Geometry.Parse("M5.5 9.5L12 16l6.5-6.5");

    public static Geometry Lock => Geometry.Parse("M7 10V7.5a5 5 0 0 1 10 0V10 M5 10h14v10H5z M12 14v2.5");

    public static Geometry Folder => Geometry.Parse("M3 6.5h6l2 2h10v10.5H3z");

    public static Geometry Archive => Geometry.Parse("M4 5h16v5H4z M5.5 10v9h13v-9 M9 14h6");

    public static Geometry Keyboard => Geometry.Parse("M3 7h18v10H3z M6 10h1 M9.5 10h1 M13 10h1 M16.5 10h1 M6 13h1 M9.5 13h1 M13 13h1 M16.5 13h1 M8 15h8");

    public static Geometry Info => Geometry.Parse("M12 21a9 9 0 1 1 0-18 9 9 0 1 1 0 18z M12 10.5V16 M12 7.5v.5");

    public static Geometry ShieldCheck => Geometry.Parse("M12 3l7 3v5c0 4.5-3 7.5-7 10-4-2.5-7-5.5-7-10V6z M8.7 12l2.2 2.2 4.4-4.6");

    public static Geometry Trash => Geometry.Parse("M5 7h14 M9 7V4h6v3 M7 7l1 13h8l1-13 M10 10.5v6 M14 10.5v6");

    public static Geometry ArrowUpRight => Geometry.Parse("M7 17L17 7 M9 7h8v8");

    public static Geometry Search => Geometry.Parse("M11 18a7 7 0 1 1 0-14 7 7 0 1 1 0 14z M16.2 16.2L21 21");
}
