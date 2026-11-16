using System.Reflection;

namespace Shop357.UI;

/// <summary>The shop logo (built into the program) and the window icon. Both are null if loading fails, so the app still runs.</summary>
public static class Branding
{
    public static readonly Image Logo = LoadLogo();
    public static readonly Icon AppIcon = LoadIcon();

    static Image LoadLogo()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
            if (name == null) return null;

            using var stream = asm.GetManifestResourceStream(name);
            using var raw = Image.FromStream(stream);
            return new Bitmap(raw); // copy, so the stream can be closed
        }
        catch
        {
            return null;
        }
    }

    // The .exe carries the icon (see ApplicationIcon in Shop357.csproj); reuse it for the windows.
    static Icon LoadIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>A square picture box showing the logo, or null when the logo is not available.</summary>
    public static PictureBox Picture(int size) => Logo == null ? null : new PictureBox
    {
        Image = Logo,
        SizeMode = PictureBoxSizeMode.Zoom,
        Size = new Size(size, size),
        BackColor = Color.Transparent,
    };
}
