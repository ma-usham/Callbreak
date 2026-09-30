# Prints the title and text of a modal dialog the Unity Editor is showing, or nothing.
# A dialog blocks the Editor's main thread, so every `unity command` times out until someone
# answers it. With -DismissTestScene it answers "Don't Save" to a "Scene(s) Have Been Modified"
# prompt, but only when every scene it lists is one of the Test Framework's own InitTestScene files.
param([switch]$DismissTestScene)

$source = @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class EditorDialog {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h, int id);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, StringBuilder l);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  const uint WM_GETTEXT = 0x000D, BM_CLICK = 0x00F5;

  static string Text(IntPtr h) { var s = new StringBuilder(4000); SendMessage(h, WM_GETTEXT, (IntPtr)4000, s); return s.ToString(); }

  public static List<string> Inspect(uint pid, bool dismissTestScene) {
    var lines = new List<string>();
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      var c = new StringBuilder(100); GetClassName(h, c, 100);
      if (p != pid || !IsWindowVisible(h) || c.ToString() != "#32770") return true;
      string title = Text(h);
      lines.Add("dialog: " + title);
      EnumChildWindows(h, (ch, l2) => { string t = Text(ch).Trim(); if (t.Length > 0) lines.Add("  " + t.Replace("\r\n", " | ")); return true; }, IntPtr.Zero);
      if (dismissTestScene && title == "Scene(s) Have Been Modified") {
        string[] body = Text(GetDlgItem(h, 1019)).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int scenes = 0, testScenes = 0;
        for (int i = 1; i < body.Length; i++) {
          string line = body[i].Trim();
          if (line.StartsWith("Your changes")) break;
          scenes++;
          if (line.StartsWith("InitTestScene")) testScenes++;
        }
        if (scenes > 0 && scenes == testScenes) { PostMessage(GetDlgItem(h, 3), BM_CLICK, IntPtr.Zero, IntPtr.Zero); lines.Add("  -> answered Don't Save (only test scenes listed)"); }
        else lines.Add("  -> left alone (it lists scenes that are not test scenes)");
      }
      return true;
    }, IntPtr.Zero);
    return lines;
  }
}
'@
Add-Type -TypeDefinition $source
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
foreach ($unity in Get-Process Unity -ErrorAction SilentlyContinue) {
  # Only the Editor that has this project open.
  if ($unity.MainWindowTitle -and $unity.MainWindowTitle -notlike "$(Split-Path $project -Leaf) - *") { continue }
  [EditorDialog]::Inspect([uint32]$unity.Id, [bool]$DismissTestScene)
}
