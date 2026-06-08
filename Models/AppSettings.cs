namespace DofusSwitcher.Models;

public class AppSettings
{
    public uint ModifierKeys { get; set; } = 0x0002; // MOD_CONTROL
    public uint HotKey { get; set; } = 0x09;         // VK_TAB
    public string HotKeyDisplay { get; set; } = "Ctrl + Tab";
    public List<CharacterOrder> PlayOrder { get; set; } = new();
    public List<Preset> Presets { get; set; } = new();
    public bool MinimizeToTray { get; set; } = true;
    public string? LastPresetName { get; set; } = null;
    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public bool OverlayVisible { get; set; } = false;
    public bool OverlayExpanded { get; set; } = false;
}

public class CharacterOrder
{
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsFemale { get; set; } = false;
    public bool BreedIcon { get; set; } = false;
}

public class Preset
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Index de l'icône dans sprites/presets/ (-1 = aucune icône).</summary>
    public int IconIndex { get; set; } = -1;

    /// <summary>
    /// Ordre de jeu sauvegardé : index = position, CharacterName/Class = identifiant.
    /// Contient aussi IsFemale/CreatureMode pour restaurer le sprite complet.
    /// </summary>
    public List<CharacterOrder> Characters { get; set; } = new();
}
