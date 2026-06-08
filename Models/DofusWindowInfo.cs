using System.ComponentModel;
using System.Runtime.CompilerServices;
using DofusSwitcher.Services;

namespace DofusSwitcher.Models;

public class DofusWindowInfo : INotifyPropertyChanged
{
    private int _playOrder;
    private bool _isActive;
    private bool _isFemale;
    private bool _breedIcon;
    private System.Windows.Media.ImageSource? _spriteSource;
    private bool _spriteLoaded;

    // ── Données fenêtre ──────────────────────────────────────
    public IntPtr Handle { get; set; }
    public string RawTitle { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int ProcessId { get; set; }

    // ── Affichage / ordre ────────────────────────────────────
    public int PlayOrder
    {
        get => _playOrder;
        set { _playOrder = value; OnPropertyChanged(); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    // ── Settings personnage ──────────────────────────────────
    /// <summary>Féminin = true → Head_XX1, Masculin = false → Head_XX0</summary>
    public bool IsFemale
    {
        get => _isFemale;
        set { _isFemale = value; InvalidateSprite(); OnPropertyChanged(); }
    }

    /// <summary>Mode icône de classe → symbol_X à la place du head</summary>
    public bool BreedIcon
    {
        get => _breedIcon;
        set { _breedIcon = value; InvalidateSprite(); OnPropertyChanged(); }
    }

    // ── Sprite ───────────────────────────────────────────────
    /// <summary>
    /// ImageSource du sprite chargé depuis sprites/{classe}/{fichier}.
    /// Null si classe inconnue ou fichier absent.
    /// Mis en cache, recalculé si IsFemale ou CreatureMode change.
    /// </summary>
    public System.Windows.Media.ImageSource? SpriteSource
    {
        get
        {
            if (!_spriteLoaded)
            {
                _spriteLoaded = true;
                _spriteSource = SpriteHelper.LoadSprite(CharacterClass, _isFemale, _breedIcon);
            }
            return _spriteSource;
        }
    }

    /// <summary>Indique si un sprite a pu être chargé (pour basculer fallback/image en XAML).</summary>
    public bool HasSprite => SpriteSource != null;

    // ── Couleur fallback (si pas de sprite) ──────────────────
    public string ClassColorHex => GetClassColor(CharacterClass);

    public System.Windows.Media.Brush ClassBrush
    {
        get
        {
            try
            {
                var color = (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(ClassColorHex);
                var brush = new System.Windows.Media.SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch { return System.Windows.Media.Brushes.Gray; }
        }
    }

    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Met à jour les données depuis une détection fraîche (même handle).
    /// Préserve l'ordre, IsFemale, CreatureMode et IsActive.
    /// </summary>
    public void UpdateFrom(DofusWindowInfo fresh)
    {
        RawTitle = fresh.RawTitle;
        Version  = fresh.Version;

        bool nameChanged  = CharacterName  != fresh.CharacterName;
        bool classChanged = !string.Equals(CharacterClass, fresh.CharacterClass, StringComparison.OrdinalIgnoreCase);

        if (nameChanged)
        {
            CharacterName = fresh.CharacterName;
            OnPropertyChanged(nameof(CharacterName));
        }

        if (classChanged)
        {
            CharacterClass = fresh.CharacterClass;
            OnPropertyChanged(nameof(CharacterClass));
            OnPropertyChanged(nameof(ClassColorHex));
            OnPropertyChanged(nameof(ClassBrush));
            InvalidateSprite(); // recharge le sprite pour la nouvelle classe
        }
    }

    private void InvalidateSprite()
    {
        _spriteLoaded = false;
        _spriteSource = null;
        OnPropertyChanged(nameof(SpriteSource));
        OnPropertyChanged(nameof(HasSprite));
    }

    private static string GetClassColor(string cls) =>
        cls.ToLowerInvariant().Trim() switch
        {
            "éliotrope" or "eliotrope" => "#9B59B6",
            "sacrieur"                  => "#E74C3C",
            "enutrof"                   => "#F39C12",
            "pandawa"                   => "#3498DB",
            "crâ" or "cra"              => "#2ECC71",
            "iop"                       => "#E67E22",
            "féca" or "feca"            => "#1ABC9C",
            "osamodas"                  => "#27AE60",
            "eniripsa"                  => "#FF69B4",
            "écaflip" or "ecaflip"      => "#F1C40F",
            "sram"                      => "#95A5A6",
            "sadida"                    => "#16A085",
            "xélor" or "xelor"          => "#2980B9",
            "roublard"                  => "#8E44AD",
            "zobal"                     => "#E91E63",
            "steamer"                   => "#607D8B",
            "huppermage"                => "#FF5722",
            "ouginak"                   => "#795548",
            "forgelance"                => "#B0BEC5",
            _                           => "#78909C"
        };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
