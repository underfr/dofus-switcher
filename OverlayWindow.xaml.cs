using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DofusSwitcher.Models;

namespace DofusSwitcher;

public partial class OverlayWindow : Window
{
    private readonly ObservableCollection<DofusWindowInfo> _windows;
    private readonly Action<DofusWindowInfo> _activateAction;
    private readonly Action<DofusWindowInfo, bool> _moveAction; // bool = true → monter
    private Action<Preset>? _applyPresetAction;
    private bool _expanded;

    // Caractères Segoe MDL2 Assets
    private const string IconChevronUp   = "";
    private const string IconChevronDown = "";

    /// <summary>Levé quand l'overlay se masque (bouton ✕).</summary>
    public event Action? VisibilityChanged;

    /// <summary>Levé après un drag, avec la nouvelle position.</summary>
    public event Action<double, double>? PositionSaved;

    public OverlayWindow(
        ObservableCollection<DofusWindowInfo> windows,
        Action<DofusWindowInfo> activateAction,
        Action<DofusWindowInfo, bool> moveAction)
    {
        InitializeComponent();
        _windows        = windows;
        _activateAction = activateAction;
        _moveAction     = moveAction;

        overlayList.ItemsSource = _windows;
    }

    // ──────────────────────────────────────────────
    // Presets
    // ──────────────────────────────────────────────

    public void SetPresets(IList<Preset> presets, Action<Preset> applyAction)
    {
        _applyPresetAction    = applyAction;
        icPresets.ItemsSource = presets;
        btnPresets.Visibility = presets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdatePresetIcon(int iconIndex)
    {
        var src = iconIndex >= 0 ? Services.SpriteHelper.LoadPresetIcon(iconIndex) : null;
        if (src != null)
        {
            imgPresetIcon.Source        = src;
            imgPresetIcon.Visibility    = Visibility.Visible;
            tbPresetFallback.Visibility = Visibility.Collapsed;
        }
        else
        {
            imgPresetIcon.Visibility    = Visibility.Collapsed;
            tbPresetFallback.Visibility = Visibility.Visible;
        }
    }

    // ──────────────────────────────────────────────
    // Position
    // ──────────────────────────────────────────────

    public void RestorePosition(double left, double top)
    {
        if (!double.IsNaN(left) && !double.IsNaN(top))
        {
            Left = left;
            Top  = top;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 20;
            Top  = area.Top   + 20;
        }
    }

    // ──────────────────────────────────────────────
    // État expand / collapse
    // ──────────────────────────────────────────────

    public void SetExpanded(bool expanded)
    {
        _expanded                = expanded;
        expandedPanel.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        btnToggle.Content        = _expanded ? IconChevronUp : IconChevronDown;
    }

    // ──────────────────────────────────────────────
    // Mise à jour du header (perso actif)
    // ──────────────────────────────────────────────

    public void UpdateActiveWindow(DofusWindowInfo? active)
    {
        if (active != null)
        {
            imgActiveSprite.Source     = active.SpriteSource;
            imgActiveSprite.Visibility = active.HasSprite ? Visibility.Visible  : Visibility.Collapsed;
            dotActive.Fill             = active.ClassBrush;
            dotActive.Visibility       = active.HasSprite ? Visibility.Collapsed : Visibility.Visible;
            tbActiveName.Text          = active.CharacterName;
            tbActiveClass.Text         = active.CharacterClass;
        }
        else
        {
            imgActiveSprite.Visibility = Visibility.Collapsed;
            dotActive.Visibility       = Visibility.Collapsed;
            tbActiveName.Text          = "Aucune fenêtre";
            tbActiveClass.Text         = "";
        }
    }

    // ──────────────────────────────────────────────
    // Drag
    // ──────────────────────────────────────────────

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        DragMove();
        PositionSaved?.Invoke(Left, Top);
    }

    // ──────────────────────────────────────────────
    // Boutons header
    // ──────────────────────────────────────────────

    private void BtnToggle_Click(object sender, RoutedEventArgs e) => SetExpanded(!_expanded);

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        VisibilityChanged?.Invoke();
    }

    private void BtnPresets_Click(object sender, RoutedEventArgs e)
    {
        presetPopup.IsOpen = !presetPopup.IsOpen;
    }

    private void PresetItem_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is Preset preset)
        {
            presetPopup.IsOpen = false;
            _applyPresetAction?.Invoke(preset);
        }
    }

    // ──────────────────────────────────────────────
    // Actions sur la liste
    // ──────────────────────────────────────────────

    private void OverlayActivate_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is DofusWindowInfo item)
            _activateAction(item);
    }

    private void OverlayMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is DofusWindowInfo item)
            _moveAction(item, true);
    }

    private void OverlayMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is DofusWindowInfo item)
            _moveAction(item, false);
    }
}
