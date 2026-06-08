using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DofusSwitcher.Models;
using DofusSwitcher.Services;

namespace DofusSwitcher;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT   = 0x0000;

    private readonly ObservableCollection<DofusWindowInfo> _windows = new();
    private readonly HotkeyManager _hotkeyManager = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private AppSettings _settings = new();
    private bool _capturingHotkey;
    private int _pendingIconIndex = -1;   // Icône sélectionnée pour le preset en cours de création
    private int _activePresetIconIndex = -1; // Icône du preset actuellement chargé
    private bool _initialLoadDone;        // Vrai après le premier RefreshWindows
    private OverlayWindow? _overlay;
    private IntPtr _winEventHook;
    private WinEventDelegate? _winEventProc; // Référence pour éviter le GC

    public MainWindow()
    {
        InitializeComponent();
        windowsList.ItemsSource = _windows;
        _windows.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _settings = SettingsManager.Load();
        _hotkeyManager.Initialize(this);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

        LoadWindowIcon();
        RegisterHotkey();
        UpdateHotkeyButton();
        PopulateIconPicker();
        RefreshPresetsComboBox();
        InitOverlay();
        RegisterForegroundHook();

        _refreshTimer.Interval = TimeSpan.FromSeconds(2.5);
        _refreshTimer.Tick += (_, _) => RefreshWindows();
        _refreshTimer.Start();

        RefreshWindows();
    }

    // ══════════════════════════════════════════════════════════
    // PRINCIPE : _windows[0] = joue en 1er, _windows[1] en 2ème…
    // PlayOrder est UNIQUEMENT un badge d'affichage (index + 1).
    // On ne trie JAMAIS par PlayOrder pour déterminer le switch.
    // ══════════════════════════════════════════════════════════

    // ──────────────────────────────────────────────
    // Détection & rafraîchissement
    // ──────────────────────────────────────────────

    private async void RefreshWindows()
    {
        var detected = await System.Threading.Tasks.Task.Run(WindowDetector.Detect);

        // 1. Retirer uniquement les fenêtres vraiment fermées (handle invalide).
        //    On NE supprime PAS les fenêtres absentes de detected : elles peuvent
        //    être en cours de chargement (titre temporairement invalide).
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            if (!IsWindow(_windows[i].Handle))
                _windows.RemoveAt(i);
        }

        // 2. Mettre à jour ou ajouter les fenêtres détectées
        var fg = WindowSwitcher.GetCurrentForeground();
        bool characterChanged = false;

        foreach (var d in detected)
        {
            var existing = _windows.FirstOrDefault(w => w.Handle == d.Handle);

            if (existing != null)
            {
                // Fenêtre connue : met à jour si le titre a changé
                if (existing.RawTitle != d.RawTitle)
                {
                    bool identityChanged =
                        existing.CharacterName != d.CharacterName ||
                        !string.Equals(existing.CharacterClass, d.CharacterClass,
                                       StringComparison.OrdinalIgnoreCase);

                    // Met à jour nom / classe / sprite
                    existing.UpdateFrom(d);

                    if (identityChanged)
                    {
                        // Restaure IsFemale, BreedIcon et position pour le nouveau perso
                        int savedIdx = _settings.PlayOrder.FindIndex(
                            p => p.CharacterName == d.CharacterName &&
                                 p.CharacterClass == d.CharacterClass);

                        if (savedIdx >= 0)
                        {
                            var saved = _settings.PlayOrder[savedIdx];
                            existing.IsFemale     = saved.IsFemale;
                            existing.BreedIcon = saved.BreedIcon;

                            // Déplace à la position sauvegardée si différente
                            int currentPos = _windows.IndexOf(existing);
                            int targetPos  = Math.Min(savedIdx, _windows.Count - 1);
                            if (currentPos >= 0 && currentPos != targetPos)
                                _windows.Move(currentPos, targetPos);
                        }

                        characterChanged = true;
                    }

                    // Notifie l'overlay immédiatement si c'est la fenêtre active
                    // (pas de nouvel événement WinEvent quand elle était déjà au premier plan)
                    if (existing.Handle == fg)
                        _overlay?.UpdateActiveWindow(existing);
                }
                continue;
            }

            // Nouvelle fenêtre → restaure position + settings sauvegardés
            int newSavedIndex = _settings.PlayOrder.FindIndex(
                p => p.CharacterName == d.CharacterName && p.CharacterClass == d.CharacterClass);

            if (newSavedIndex >= 0)
            {
                var saved = _settings.PlayOrder[newSavedIndex];
                d.IsFemale     = saved.IsFemale;
                d.BreedIcon = saved.BreedIcon;

                int insertAt = Math.Min(newSavedIndex, _windows.Count);
                _windows.Insert(insertAt, d);
            }
            else
            {
                _windows.Add(d);
            }
        }

        if (characterChanged) SavePlayOrder();

        // 3. Mettre à jour les badges (index = position réelle, pas PlayOrder)
        Renumber();
        UpdateActiveIndicator();

        // 4. Au premier chargement : restaurer le dernier preset utilisé
        if (!_initialLoadDone)
        {
            _initialLoadDone = true;
            if (_settings.LastPresetName is { } lastName)
            {
                var last = _settings.Presets.FirstOrDefault(p => p.Name == lastName);
                if (last != null)
                {
                    // Sélectionner dans le ComboBox (déclenche CbPresets_SelectionChanged → ApplyPreset)
                    cbPresets.SelectedItem = last;
                }
            }
        }
    }

    /// <summary>
    /// Recalcule les badges d'affichage depuis la position réelle dans la collection.
    /// N'a AUCUN effet sur l'ordre de switch — c'est uniquement visuel.
    /// </summary>
    private void Renumber()
    {
        for (int i = 0; i < _windows.Count; i++)
            _windows[i].PlayOrder = i + 1;
    }

    private void UpdateActiveIndicator()
    {
        var fg = WindowSwitcher.GetCurrentForeground();

        // Si la fenêtre au premier plan n'est pas une fenêtre Dofus (ex : overlay,
        // fenêtre principale, autre appli), on ne touche pas à l'état actif connu.
        var active = _windows.FirstOrDefault(w => w.Handle == fg);
        if (active == null) return;

        foreach (var w in _windows)
            w.IsActive = w.Handle == fg;

        tbCurrentWindow.Text = $"Actif : {active.CharacterName}  ({active.PlayOrder}/{_windows.Count})";
        bdCurrentWindow.Visibility = Visibility.Visible;
        _overlay?.UpdateActiveWindow(active);
    }

    private void UpdateEmptyState() =>
        tbEmpty.Visibility = _windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    // ──────────────────────────────────────────────
    // Hotkey — switch de fenêtre
    // ──────────────────────────────────────────────

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            if (_windows.Count == 0) return;

            // On cherche la fenêtre Dofus actuellement au premier plan
            var fg = WindowSwitcher.GetCurrentForeground();
            int currentIdx = -1;
            for (int i = 0; i < _windows.Count; i++)
            {
                if (_windows[i].Handle == fg) { currentIdx = i; break; }
            }

            // On passe à la suivante dans la collection (cycle)
            int nextIdx = currentIdx >= 0 ? (currentIdx + 1) % _windows.Count : 0;
            var target = _windows[nextIdx];

            WindowSwitcher.Activate(target);

            foreach (var w in _windows)
                w.IsActive = w.Handle == target.Handle;

            tbCurrentWindow.Text = $"Actif : {target.CharacterName}  ({target.PlayOrder}/{_windows.Count})";
            bdCurrentWindow.Visibility = Visibility.Visible;
            _overlay?.UpdateActiveWindow(target);
        });
    }

    // ──────────────────────────────────────────────
    // Boutons UI — réorganisation de l'ordre
    // ──────────────────────────────────────────────

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshWindows();

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DofusWindowInfo item) return;
        int idx = _windows.IndexOf(item);
        if (idx <= 0) return;

        _windows.Move(idx, idx - 1);   // Déplace dans la collection → nouvel ordre de switch
        Renumber();                     // Met à jour les badges d'affichage
        SavePlayOrder();                // Persiste le nouvel ordre
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DofusWindowInfo item) return;
        int idx = _windows.IndexOf(item);
        if (idx < 0 || idx >= _windows.Count - 1) return;

        _windows.Move(idx, idx + 1);
        Renumber();
        SavePlayOrder();
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DofusWindowInfo item) return;
        WindowSwitcher.Activate(item);
        foreach (var w in _windows) w.IsActive = w.Handle == item.Handle;
        UpdateActiveIndicator();
    }

    private void SetMale_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DofusWindowInfo item) return;
        item.IsFemale = false;
        SavePlayOrder();
    }

    private void SetFemale_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DofusWindowInfo item) return;
        item.IsFemale = true;
        SavePlayOrder();
    }

    private void ToggleBreedIcon_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DofusWindowInfo item) return;
        item.BreedIcon = !item.BreedIcon;
        SavePlayOrder();
    }

    /// <summary>
    /// Sauvegarde l'ordre de jeu = liste ordonnée des personnages.
    /// L'index dans la liste = position dans _windows = ordre de switch.
    /// Inclut aussi les settings genre/créature par personnage.
    /// </summary>
    private void SavePlayOrder()
    {
        // État actuel des fenêtres connectées
        var current = _windows.Select((w, i) => new CharacterOrder
        {
            CharacterName  = w.CharacterName,
            CharacterClass = w.CharacterClass,
            Order          = i,
            IsFemale       = w.IsFemale,
            BreedIcon      = w.BreedIcon,
        }).ToList();

        // Conserve les paramètres (genre / icône) des persos non connectés
        // pour qu'ils soient restaurés correctement à leur prochaine connexion.
        // Évite que SavePlayOrder() appelé sans fenêtres (ex: chargement du preset
        // au démarrage) n'écrase les réglages sauvegardés avec une liste vide.
        foreach (var old in _settings.PlayOrder)
        {
            bool isOpen = current.Any(c =>
                c.CharacterName == old.CharacterName &&
                c.CharacterClass == old.CharacterClass);
            if (!isOpen)
                current.Add(old);
        }

        _settings.PlayOrder = current;
        SettingsManager.Save(_settings);
    }

    // ──────────────────────────────────────────────
    // Détection foreground (switch manuel)
    // ──────────────────────────────────────────────

    private void RegisterForegroundHook()
    {
        // On garde une référence managée pour que le GC ne la collecte pas
        _winEventProc = OnForegroundWindowChanged;
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc,
            0, 0, WINEVENT_OUTOFCONTEXT);
    }

    /// <summary>
    /// Appelé (sur le thread UI) à chaque changement de fenêtre au premier plan.
    /// Met à jour l'indicateur actif uniquement si la nouvelle fenêtre est une fenêtre Dofus.
    /// </summary>
    private void OnForegroundWindowChanged(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        var dofusWin = _windows.FirstOrDefault(w => w.Handle == hwnd);
        if (dofusWin == null) return; // pas une fenêtre Dofus → on ignore

        foreach (var w in _windows)
            w.IsActive = w.Handle == hwnd;

        tbCurrentWindow.Text = $"Actif : {dofusWin.CharacterName}  ({dofusWin.PlayOrder}/{_windows.Count})";
        bdCurrentWindow.Visibility = Visibility.Visible;
        _overlay?.UpdateActiveWindow(dofusWin);
    }

    // ──────────────────────────────────────────────
    // Overlay flottant
    // ──────────────────────────────────────────────

    private void InitOverlay()
    {
        _overlay = new OverlayWindow(
            _windows,
            activateAction: item =>
            {
                WindowSwitcher.Activate(item);
                foreach (var w in _windows) w.IsActive = w.Handle == item.Handle;
                UpdateActiveIndicator();
            },
            moveAction: (item, moveUp) =>
            {
                int idx = _windows.IndexOf(item);
                if (moveUp)
                {
                    if (idx > 0) { _windows.Move(idx, idx - 1); Renumber(); SavePlayOrder(); }
                }
                else
                {
                    if (idx >= 0 && idx < _windows.Count - 1) { _windows.Move(idx, idx + 1); Renumber(); SavePlayOrder(); }
                }
            }
        );

        _overlay.PositionSaved += (x, y) =>
        {
            _settings.OverlayLeft = x;
            _settings.OverlayTop  = y;
            SettingsManager.Save(_settings);
        };

        SyncOverlayPresets();

        _overlay.VisibilityChanged += () =>
        {
            _settings.OverlayVisible = false;
            SettingsManager.Save(_settings);
            UpdateOverlayButton();
        };

        _overlay.RestorePosition(_settings.OverlayLeft, _settings.OverlayTop);
        _overlay.SetExpanded(_settings.OverlayExpanded);

        if (_settings.OverlayVisible)
            _overlay.Show();

        UpdateOverlayButton();
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null) return;

        if (_overlay.IsVisible)
        {
            _overlay.Hide();
            _settings.OverlayVisible = false;
        }
        else
        {
            _overlay.Show();
            _settings.OverlayVisible = true;
        }

        SettingsManager.Save(_settings);
        UpdateOverlayButton();
    }

    private void UpdateOverlayButton()
    {
        var visible = _overlay?.IsVisible ?? false;
        btnOverlay.Content = visible ? "◉  Overlay" : "○  Overlay";
        btnOverlay.Opacity = visible ? 1.0 : 0.6;
    }

    /// <summary>
    /// Pousse la liste de presets à jour vers l'overlay + met à jour son icône active.
    /// À appeler après chaque changement de presets ou de preset actif.
    /// </summary>
    private void SyncOverlayPresets()
    {
        if (_overlay == null) return;

        _overlay.SetPresets(_settings.Presets, preset =>
        {
            // Applique le preset
            ApplyPreset(preset);

            // Synchronise le ComboBox sans re-déclencher CbPresets_SelectionChanged
            cbPresets.SelectionChanged -= CbPresets_SelectionChanged;
            cbPresets.SelectedItem      = _settings.Presets.FirstOrDefault(p => p.Name == preset.Name);
            cbPresets.SelectionChanged += CbPresets_SelectionChanged;

            tbPresetName.Text = preset.Name;
            _pendingIconIndex = preset.IconIndex;
            UpdateIconPreviewButton();
        });

        _overlay.UpdatePresetIcon(_activePresetIconIndex);
    }

    // ──────────────────────────────────────────────
    // Icône application
    // ──────────────────────────────────────────────

    private void LoadWindowIcon()
    {
        var src = Services.SpriteHelper.LoadAppIcon();
        if (src != null) Icon = src;
    }

    // ──────────────────────────────────────────────
    // Presets
    // ──────────────────────────────────────────────

    private void PopulateIconPicker()
    {
        int count = Services.SpriteHelper.PresetIconCount();
        icIconPicker.ItemsSource = Enumerable.Range(0, count).ToList();
    }

    private void RefreshPresetsComboBox()
    {
        var selectedName = (cbPresets.SelectedItem as Models.Preset)?.Name;
        cbPresets.ItemsSource = null;
        cbPresets.ItemsSource = _settings.Presets;

        if (selectedName != null)
            cbPresets.SelectedItem = _settings.Presets.FirstOrDefault(p => p.Name == selectedName);

        if (_settings.Presets.Count == 0)
            tbPresetName.Text = "Nom du preset...";
    }

    private void CbPresets_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (cbPresets.SelectedItem is not Models.Preset preset) return;

        // Met à jour le champ nom + icône pending
        tbPresetName.Text = preset.Name;
        _pendingIconIndex = preset.IconIndex;
        UpdateIconPreviewButton();

        // Charge le preset immédiatement
        ApplyPreset(preset);
    }

    private void TbPresetName_GotFocus(object sender, RoutedEventArgs e)
    {
        if (tbPresetName.Text == "Nom du preset...")
            tbPresetName.Text = "";
    }

    // ── Icon picker ───────────────────────────────

    private void BtnPickIcon_Click(object sender, RoutedEventArgs e)
    {
        iconPickerPopup.IsOpen = !iconPickerPopup.IsOpen;
    }

    private void IconPicker_Select(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is int idx)
        {
            _pendingIconIndex = idx;
            UpdateIconPreviewButton();
            iconPickerPopup.IsOpen = false;
        }
    }

    private void UpdateIconPreviewButton()
    {
        imgIconPreview.Source = _pendingIconIndex >= 0
            ? Services.SpriteHelper.LoadPresetIcon(_pendingIconIndex)
            : null;

        // Teinte du bouton : actif si icône sélectionnée
        btnPickIcon.Opacity = _pendingIconIndex >= 0 ? 1.0 : 0.45;
    }

    /// <summary>Met à jour l'icône du preset affiché dans le header.</summary>
    private void UpdateHeaderPresetIcon()
    {
        var src = _activePresetIconIndex >= 0
            ? Services.SpriteHelper.LoadPresetIcon(_activePresetIconIndex)
            : null;

        imgActivePreset.Source     = src;
        imgActivePreset.Visibility = src != null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Sauvegarde ────────────────────────────────

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var name = tbPresetName.Text.Trim();
        if (string.IsNullOrEmpty(name) || name == "Nom du preset...") return;

        var characters = _windows.Select((w, i) => new Models.CharacterOrder
        {
            CharacterName  = w.CharacterName,
            CharacterClass = w.CharacterClass,
            Order          = i,
            IsFemale       = w.IsFemale,
            BreedIcon   = w.BreedIcon,
        }).ToList();

        var existing = _settings.Presets.FirstOrDefault(p => p.Name == name);
        if (existing != null)
        {
            existing.Characters = characters;
            existing.IconIndex  = _pendingIconIndex;
        }
        else
        {
            _settings.Presets.Add(new Models.Preset
            {
                Name       = name,
                IconIndex  = _pendingIconIndex,
                Characters = characters
            });
        }

        SettingsManager.Save(_settings);
        RefreshPresetsComboBox();
        cbPresets.SelectedItem = _settings.Presets.FirstOrDefault(p => p.Name == name);
        SyncOverlayPresets();
    }

    private void ApplyPreset(Models.Preset preset)
    {
        // Réordonner _windows selon le preset
        var ordered = new List<DofusWindowInfo>();

        foreach (var pc in preset.Characters.OrderBy(c => c.Order))
        {
            var win = _windows.FirstOrDefault(
                w => w.CharacterName == pc.CharacterName && w.CharacterClass == pc.CharacterClass);
            if (win == null) continue;

            win.IsFemale     = pc.IsFemale;
            win.BreedIcon = pc.BreedIcon;
            ordered.Add(win);
        }

        // Les fenêtres non présentes dans le preset vont en fin de liste
        foreach (var w in _windows)
            if (!ordered.Contains(w)) ordered.Add(w);

        // Reconstruction de la collection dans le nouvel ordre
        _windows.Clear();
        foreach (var w in ordered) _windows.Add(w);

        Renumber();

        // Sync le PlayOrder pour les persos du preset non connectés :
        // leurs réglages genre/icône du preset sont enregistrés maintenant
        // pour être restaurés dès leur prochaine connexion.
        foreach (var pc in preset.Characters)
        {
            bool isOpen = ordered.Any(w =>
                w.CharacterName == pc.CharacterName && w.CharacterClass == pc.CharacterClass);
            if (isOpen) continue;

            int idx = _settings.PlayOrder.FindIndex(p =>
                p.CharacterName == pc.CharacterName && p.CharacterClass == pc.CharacterClass);
            var entry = new CharacterOrder
            {
                CharacterName  = pc.CharacterName,
                CharacterClass = pc.CharacterClass,
                Order          = pc.Order,
                IsFemale       = pc.IsFemale,
                BreedIcon      = pc.BreedIcon,
            };
            if (idx >= 0) _settings.PlayOrder[idx] = entry;
            else          _settings.PlayOrder.Add(entry);
        }

        SavePlayOrder();

        // Affiche l'icône du preset chargé dans le header + overlay
        _activePresetIconIndex = preset.IconIndex;
        UpdateHeaderPresetIcon();
        _overlay?.UpdatePresetIcon(_activePresetIconIndex);

        // Mémorise le dernier preset utilisé
        _settings.LastPresetName = preset.Name;
        SettingsManager.Save(_settings);
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (cbPresets.SelectedItem is not Models.Preset preset) return;

        // Si on supprime le preset dont l'icône est affichée dans le header, on efface
        if (preset.IconIndex == _activePresetIconIndex)
        {
            _activePresetIconIndex = -1;
            UpdateHeaderPresetIcon();
        }

        _settings.Presets.Remove(preset);
        SettingsManager.Save(_settings);
        tbPresetName.Text = "Nom du preset...";
        RefreshPresetsComboBox();
        SyncOverlayPresets();
    }

    // ──────────────────────────────────────────────
    // Configuration hotkey
    // ──────────────────────────────────────────────

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey) return;
        _capturingHotkey = true;
        _hotkeyManager.Unregister();

        tbHotkeyBtnText.Text = "En attente...";
        btnHotkey.IsEnabled = false;
        bdCapture.Visibility = Visibility.Visible;
        tbCapture.Text = "Appuyez sur votre combinaison de touches...";
        tbCapture.Focus();
        tbHotkeyStatusIcon.Text = "";
        tbHotkeyStatus.Text = "En attente";
        tbHotkeyStatus.Foreground = System.Windows.Media.Brushes.Orange;
    }

    private void TbCapture_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturingHotkey) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignorer les touches modificatrices seules
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        uint mods = 0;
        string display = "";

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        { mods |= 0x0002; display += "Ctrl + "; }

        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
        { mods |= 0x0001; display += "Alt + "; }

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        { mods |= 0x0004; display += "Shift + "; }

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        display += key.ToString();

        _settings.ModifierKeys = mods;
        _settings.HotKey = vk;
        _settings.HotKeyDisplay = display;
        SettingsManager.Save(_settings);

        FinishCapture();
    }

    private void TbCapture_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey) FinishCapture();
    }

    private void FinishCapture()
    {
        _capturingHotkey = false;
        bdCapture.Visibility = Visibility.Collapsed;
        btnHotkey.IsEnabled = true;
        UpdateHotkeyButton();
        RegisterHotkey();
    }

    private void RegisterHotkey()
    {
        bool ok = _hotkeyManager.Register(_settings.ModifierKeys, _settings.HotKey);
        tbHotkeyStatusIcon.Text       = ok ? "" : "";
        tbHotkeyStatusIcon.Foreground = ok ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed;
        tbHotkeyStatus.Text           = ok ? "Actif" : "Conflit";
        tbHotkeyStatus.Foreground     = ok ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed;
    }

    private void UpdateHotkeyButton() =>
        tbHotkeyBtnText.Text = $"{_settings.HotKeyDisplay} - Changer";

    // ──────────────────────────────────────────────
    // Cycle de vie fenêtre
    // ──────────────────────────────────────────────

    // ──────────────────────────────────────────────
    // Titlebar personnalisé
    // ──────────────────────────────────────────────

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Ne pas déclencher le drag si le clic vient d'un bouton ou de l'un de ses enfants
        var src = e.OriginalSource as DependencyObject;
        while (src != null)
        {
            if (src is System.Windows.Controls.Button) return;
            src = VisualTreeHelper.GetParent(src);
        }
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Bouton ✕ → fermeture réelle : on laisse OnClosed faire le ménage
        // (pas de e.Cancel = true ici)
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        _hotkeyManager.Dispose();

        if (_winEventHook != IntPtr.Zero)
            UnhookWinEvent(_winEventHook);

        if (_overlay != null)
            _overlay.Close();

        base.OnClosed(e);
        System.Windows.Application.Current.Shutdown();
    }
}
