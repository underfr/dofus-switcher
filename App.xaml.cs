using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace DofusSwitcher;

public partial class App : Application
{
    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "Dofus Switcher"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Afficher / Masquer", null, (_, _) => ToggleWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => { _notifyIcon.Visible = false; Shutdown(); });
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleWindow();

        _mainWindow = new MainWindow();
        _mainWindow.Show();
        MainWindow = _mainWindow;
    }

    private void ToggleWindow()
    {
        if (_mainWindow == null) return;
        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }

    // Crée une icône colorée 16x16 en mémoire
    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(168, 168, 255));
        g.FillEllipse(brush, 1, 1, 13, 13);
        using var pen = new Pen(Color.FromArgb(100, 100, 200), 1.5f);
        g.DrawEllipse(pen, 1, 1, 13, 13);
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
