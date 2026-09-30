# Shared download lock and pick-up for ChatGPT downloads (Docs/Tasks/UI-Art-ChatGPT-Brief.md, "Downloads").
#   dl.ps1 lock                         take ArtSource/Working/_download.lock (waits for it)
#   dl.ps1 take -Name x.png -Dest path  move Downloads\x.png to path, then release the lock
#   dl.ps1 unlock                       release the lock without moving anything
# The Decor designer saves each image under its own name (Decor__<Asset>_aN.png), so the file to
# pick up is never ambiguous; the lock is still taken so nobody else mistakes it for theirs.
param(
    [Parameter(Mandatory, Position = 0)] [ValidateSet('lock', 'take', 'unlock')] [string] $Cmd,
    [string] $Name,
    [string] $Dest,
    [int] $TimeoutSec = 120
)
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$lock = Join-Path $root 'ArtSource\Working\_download.lock'
$downloads = Join-Path $env:USERPROFILE 'Downloads'

function Release-Lock {
    if (Test-Path $lock) { Remove-Item $lock -Force -ErrorAction SilentlyContinue }
}

switch ($Cmd) {
    'lock' {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        while ($true) {
            try {
                New-Item -ItemType Directory -Path $lock -ErrorAction Stop | Out-Null
                "locked after {0:F1}s" -f $sw.Elapsed.TotalSeconds
                break
            }
            catch {
                if ($sw.Elapsed.TotalSeconds -gt $TimeoutSec) {
                    $age = ((Get-Date) - (Get-Item $lock).CreationTime).TotalSeconds
                    throw ("download lock still held after {0}s (lock is {1:F0}s old)" -f $TimeoutSec, $age)
                }
                Start-Sleep -Milliseconds 700
            }
        }
    }
    'take' {
        try {
            $src = Join-Path $downloads $Name
            $sw = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path $src) -or (Test-Path "$src.crdownload")) {
                if ($sw.Elapsed.TotalSeconds -gt 40) { throw "download $Name did not arrive in $downloads" }
                Start-Sleep -Milliseconds 400
            }
            Start-Sleep -Milliseconds 300
            New-Item -ItemType Directory -Force (Split-Path $Dest) | Out-Null
            Move-Item -Path $src -Destination $Dest -Force
            Add-Type -AssemblyName System.Drawing
            $img = [System.Drawing.Image]::FromFile($Dest)
            "{0}  {1}x{2}  {3}  {4} bytes" -f (Split-Path $Dest -Leaf), $img.Width, $img.Height, $img.PixelFormat, (Get-Item $Dest).Length
            $img.Dispose()
        }
        finally { Release-Lock }
    }
    'unlock' { Release-Lock; "unlocked" }
}
