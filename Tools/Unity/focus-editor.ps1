# Brings the Unity Editor window for this project to the foreground for a moment, then gives
# focus back. After a package change or a long import the Editor sometimes stops servicing CLI
# commands ("Main thread operation timed out") until its window is activated once.
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CallbreakWin {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
'@
$unity = Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like 'Callbreak*' } | Select-Object -First 1
if (-not $unity) { Write-Output 'no Callbreak Editor window found'; exit 1 }
$previous = [CallbreakWin]::GetForegroundWindow()
[CallbreakWin]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [CallbreakWin]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
[void][CallbreakWin]::SetForegroundWindow($unity.MainWindowHandle)
Start-Sleep -Milliseconds 2500
if ($previous -ne [IntPtr]::Zero -and $previous -ne $unity.MainWindowHandle) {
    [CallbreakWin]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [CallbreakWin]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    [void][CallbreakWin]::SetForegroundWindow($previous)
}
Write-Output "focused Editor pid $($unity.Id)"
