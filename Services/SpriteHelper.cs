using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DofusSwitcher.Services;

/// <summary>
/// Charge les sprites embarqués dans l'exe (EmbeddedResource).
/// Nom des ressources : DofusSwitcher.sprites.{dossier}.{fichier}.png
/// </summary>
public static class SpriteHelper
{
    private static readonly Assembly Asm = Assembly.GetExecutingAssembly();
    private const string Prefix = "DofusSwitcher.sprites.";

    // Mapping : nom de classe (toutes variantes) → (sous-dossier, classId)
    private static readonly Dictionary<string, (string folder, int id)> Classes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["féca"]       = ("feca",        1),
            ["feca"]       = ("feca",        1),
            ["osamodas"]   = ("osamodas",    2),
            ["enutrof"]    = ("enutrof",     3),
            ["sram"]       = ("sram",        4),
            ["xélor"]      = ("xelor",       5),
            ["xelor"]      = ("xelor",       5),
            ["écaflip"]    = ("ecaflip",     6),
            ["ecaflip"]    = ("ecaflip",     6),
            ["eniripsa"]   = ("eniripsa",    7),
            ["iop"]        = ("iop",         8),
            ["crâ"]        = ("cra",         9),
            ["cra"]        = ("cra",         9),
            ["sadida"]     = ("sadida",     10),
            ["sacrieur"]   = ("sacrieur",   11),
            ["pandawa"]    = ("pandawa",    12),
            ["roublard"]   = ("roublard",   13),
            ["zobal"]      = ("zobal",      14),
            ["steamer"]    = ("steamer",    15),
            ["éliotrope"]  = ("eliotrope",  16),
            ["eliotrope"]  = ("eliotrope",  16),
            ["huppermage"] = ("huppermage", 17),
            ["ouginak"]    = ("ouginak",    18),
            ["forgelance"] = ("forgelance", 20),
        };

    // ──────────────────────────────────────────────
    // API publique
    // ──────────────────────────────────────────────

    /// <summary>Retourne vrai si la classe est connue (sprite potentiellement dispo).</summary>
    public static bool IsKnownClass(string className) =>
        Classes.ContainsKey(className.Trim());

    /// <summary>Charge et retourne le sprite tête/symbole d'un personnage.</summary>
    public static ImageSource? LoadSprite(string className, bool isFemale, bool breedIcon)
    {
        var key = className.Trim();
        if (!Classes.TryGetValue(key, out var info)) return null;

        string filename = breedIcon
            ? $"symbol_{info.id}.png"
            : $"Head_{info.id * 10 + (isFemale ? 1 : 0)}.png";

        return Load($"{Prefix}{info.folder}.{filename}", decodeHeight: 48);
    }

    /// <summary>Charge l'icône de preset numérotée (sprites/presets/{index}.png).</summary>
    public static ImageSource? LoadPresetIcon(int index)
    {
        if (index < 0) return null;
        return Load($"{Prefix}presets.{index}.png", decodeHeight: 32);
    }

    /// <summary>Nombre total d'icônes de presets disponibles.</summary>
    public static int PresetIconCount()
    {
        string presetPrefix = $"{Prefix}presets.";
        return Asm.GetManifestResourceNames()
                  .Count(n => n.StartsWith(presetPrefix, StringComparison.Ordinal)
                           && n.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Charge l'icône principale de l'application (sprites/icon.png).</summary>
    public static ImageSource? LoadAppIcon() =>
        Load($"{Prefix}icon.png", decodeHeight: 64);

    // ──────────────────────────────────────────────
    // Helpers privés
    // ──────────────────────────────────────────────

    private static ImageSource? Load(string resourceName, int decodeHeight)
    {
        try
        {
            using var stream = Asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource    = stream;
            bmp.CacheOption     = BitmapCacheOption.OnLoad; // lit tout pendant EndInit
            bmp.DecodePixelHeight = decodeHeight;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
