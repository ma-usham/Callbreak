# Dot-source to load the ShopBackgroundsB image helpers:  . "$PSScriptRoot\bg.ps1"
Add-Type -AssemblyName System.Drawing
if (-not ('Img' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'BgTools.cs') -ReferencedAssemblies System.Drawing
}
$global:Repo = 'D:\UnityProjects\Callbreak'
$global:Area = Join-Path $Repo 'ArtSource\Working\ShopBackgroundsB'
# The plate frame: 2230 x 1255; the 16:9 gameplay view (= the mockup, 1672 x 941) sits at (279, 157).
$global:PlateW = 2230; $global:PlateH = 1255; $global:ViewX = 279; $global:ViewY = 157; $global:ViewW = 1672; $global:ViewH = 941
