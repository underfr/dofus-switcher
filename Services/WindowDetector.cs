using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DofusSwitcher.Models;

namespace DofusSwitcher.Services;

public static class WindowDetector
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Known Dofus-related process names
    private static readonly HashSet<string> DofusProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dofus", "DofusRetro", "Dofus_Unity", "Ankama"
    };

    public static List<DofusWindowInfo> Detect()
    {
        var results = new List<DofusWindowInfo>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;

            var len = GetWindowTextLength(hwnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();

            GetWindowThreadProcessId(hwnd, out uint pid);

            string processName;
            try
            {
                processName = Process.GetProcessById((int)pid).ProcessName;
            }
            catch { return true; }

            bool isDofusProcess = IsDofusProcess(processName);
            var info = ParseTitle(title, hwnd, (int)pid, isDofusProcess);
            if (info != null) results.Add(info);

            return true;
        }, IntPtr.Zero);

        return results;
    }

    private static bool IsDofusProcess(string name) =>
        DofusProcessNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static DofusWindowInfo? ParseTitle(string title, IntPtr hwnd, int pid, bool isDofusProcess)
    {
        // Format attendu : "NomPersonnage - Classe - Version - BuildType"
        var parts = title.Split(" - ", StringSplitOptions.TrimEntries);

        // Il faut au minimum 4 parties pour un personnage chargé.
        // En dessous = fenêtre en cours de chargement ou launcher → on ignore.
        if (parts.Length < 4) return null;

        // Valide le format Dofus : la 3ème partie doit ressembler à une version (ex: 3.5.17.21)
        bool looksLikeDofusTitle = parts[2].Split('.').Length >= 2
            && parts[2].Split('.').All(p => int.TryParse(p, out _));

        if (!isDofusProcess && !looksLikeDofusTitle) return null;

        // Rejette les fenêtres de chargement dont le "nom" est simplement "Dofus"
        if (parts[0].Equals("Dofus", StringComparison.OrdinalIgnoreCase)) return null;

        // Filtre les fenêtres sans classe ou dont la classe ressemble à un numéro de version
        if (string.IsNullOrWhiteSpace(parts[1])) return null;
        if (parts[1].Contains('.') && parts[1].Split('.').All(p => int.TryParse(p, out _))) return null;

        return new DofusWindowInfo
        {
            Handle        = hwnd,
            ProcessId     = pid,
            RawTitle      = title,
            CharacterName  = parts[0],
            CharacterClass = parts[1],
            Version        = parts[2],
        };
    }
}
