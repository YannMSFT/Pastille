using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Windows.Media;

namespace Pastille;

internal sealed class Settings
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pastille");
    public double Width { get; set; } = 280;
    public double Height { get; set; } = 210;
    public double Left { get; set; } = 60;
    public double Top { get; set; } = 60;
    public bool Oval { get; set; } = true;
    public bool Mirror { get; set; } = true;
    public double Zoom { get; set; } = 1;
    public double PanX { get; set; }
    public double PanY { get; set; }
    public bool OutlineEnabled { get; set; } = true;
    public double OutlineThickness { get; set; } = 2;
    public string OutlineColor { get; set; } = "#40E0D0";
    public double CornerRadius { get; set; } = 12;
    public string CameraId { get; set; } = "";

    internal static bool TryParseColor(string? text, out Color color)
    {
        color = default;
        if (text is null || text.Length != 7 || text[0] != '#' ||
            !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            return false;
        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    public void Normalize()
    {
        Width = double.IsFinite(Width) ? Math.Clamp(Width, 120, 960) : 280;
        Height = double.IsFinite(Height) ? Math.Clamp(Height, 90, 720) : 210;
        Left = double.IsFinite(Left) ? Left : 60;
        Top = double.IsFinite(Top) ? Top : 60;
        Zoom = double.IsFinite(Zoom) ? Math.Clamp(Zoom, 0.5, 4) : 1;
        PanX = double.IsFinite(PanX) ? Math.Clamp(PanX, -1, 1) : 0;
        PanY = double.IsFinite(PanY) ? Math.Clamp(PanY, -1, 1) : 0;
        OutlineThickness = double.IsFinite(OutlineThickness) ? Math.Clamp(OutlineThickness, 1, 20) : 2;
        CornerRadius = double.IsFinite(CornerRadius) ? Math.Clamp(CornerRadius, 0, 200) : 12;
        if (!TryParseColor(OutlineColor, out _))
            throw new InvalidDataException("La couleur du liseré doit être au format #RRGGBB.");
    }

    public static Settings Load() => Load(DataDirectory, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Visage"));

    internal static Settings Load(string directory, string legacyDirectory)
    {
        var path = Path.Combine(directory, "settings.json");
        var migrating = !File.Exists(path);
        if (migrating) path = Path.Combine(legacyDirectory, "settings.json");
        if (!File.Exists(path)) return new Settings();
        var result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Le fichier de préférences est vide.");
        result.Normalize();
        if (migrating) result.Save(directory);
        return result;
    }

    public void Save() => Save(DataDirectory);

    internal void Save(string directory)
    {
        Normalize();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
