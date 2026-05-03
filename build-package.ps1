param(
    [string]$Version = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $root "artifacts"
$releaseRoot = Join-Path $artifactRoot "release"
$bundleName = "LicorpExportPlus.bundle"
$bundlePath = Join-Path $releaseRoot $bundleName
$buildPropsPath = Join-Path $root "Directory.Build.props"
$programDataBundle = "C:\ProgramData\Autodesk\ApplicationPlugins\LicorpExportPlus.bundle"
$yearMap = @{
    "2020" = "R2020"
    "2021" = "R2020"
    "2022" = "R2020"
    "2023" = "R2020"
    "2024" = "R2020"
    "2025" = "R2025"
    "2026" = "R2025"
    "2027" = "R2027"
}

function Ensure-Directory([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Read-VersionFromProps([string]$propsPath) {
    if (!(Test-Path -LiteralPath $propsPath)) {
        return "1.0.0"
    }
    [xml]$props = Get-Content -LiteralPath $propsPath
    $ver = $props.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($ver)) { return "1.0.0" }
    return $ver.Trim()
}

function Read-BuildMetadata([string]$propsPath) {
    if (!(Test-Path -LiteralPath $propsPath)) {
        return @{
            AddInId = "00000000-0000-0000-0000-000000000000"
            VendorId = "LICORP"
            VendorDescription = "Licorp"
        }
    }

    [xml]$props = Get-Content -LiteralPath $propsPath
    $pg = $props.Project.PropertyGroup | Select-Object -First 1
    function Read-Prop([object]$Group, [string]$Name, [string]$Default) {
        $node = $Group.SelectSingleNode($Name)
        if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
            return $Default
        }
        return $node.InnerText.Trim()
    }

    return @{
        AddInId = Read-Prop $pg "AddInId" "00000000-0000-0000-0000-000000000000"
        VendorId = Read-Prop $pg "VendorId" "LICORP"
        VendorDescription = Read-Prop $pg "VendorDescription" "Licorp"
    }
}

function New-RevitAddinManifest([string]$assemblyPath, [string]$addInId, [string]$vendorId, [string]$vendorDescription) {
@"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>LicorpExportPlus</Name>
    <Assembly>$assemblyPath</Assembly>
    <AddInId>$addInId</AddInId>
    <FullClassName>LicorpExportPlus.ExportPlusApplication</FullClassName>
    <VendorId>$vendorId</VendorId>
    <VendorDescription>$vendorDescription</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
}

function Copy-FolderContents([string]$Source, [string]$Destination) {
    if (!(Test-Path -LiteralPath $Source)) {
        throw "Source folder not found: $Source"
    }
    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }
    Ensure-Directory $Destination
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

Ensure-Directory $artifactRoot
Ensure-Directory $releaseRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Read-VersionFromProps $buildPropsPath
}

$meta = Read-BuildMetadata $buildPropsPath

$versionReleaseRoot = Join-Path $releaseRoot $Version
Ensure-Directory $versionReleaseRoot

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "LicorpExportPlus build/package" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host "SkipBuild: $SkipBuild"
Write-Host ""

if (-not $SkipBuild) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "deploy-bundle.ps1") -Configuration "Release" -SkipDeploy
    if ($LASTEXITCODE -ne 0) { throw "deploy-bundle.ps1 failed." }
} else {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "deploy-bundle.ps1") -Configuration "Release" -SkipDeploy -SkipBuild
    if ($LASTEXITCODE -ne 0) { throw "deploy-bundle.ps1 failed." }
}

if (!(Test-Path -LiteralPath $bundlePath)) {
    throw "Bundle not found: $bundlePath"
}

$stagingRoot = Join-Path $versionReleaseRoot "staging"
$revitStage = Join-Path $stagingRoot "revit"
$bundleStage = Join-Path $revitStage "ApplicationPlugins\$bundleName"
$addinsStage = Join-Path $revitStage "Addins"

Ensure-Directory $stagingRoot
Ensure-Directory $revitStage
Ensure-Directory $addinsStage

Copy-FolderContents -Source $bundlePath -Destination $bundleStage

foreach ($year in $yearMap.Keys) {
    $addinDir = Join-Path $addinsStage $year
    Ensure-Directory $addinDir
    $label = $yearMap[$year]
    $assemblyPath = Join-Path $programDataBundle ("Contents\" + $label + "\LicorpExportPlus.dll")
    $manifest = New-RevitAddinManifest -assemblyPath $assemblyPath -addInId $meta.AddInId -vendorId $meta.VendorId -vendorDescription $meta.VendorDescription
    Set-Content -LiteralPath (Join-Path $addinDir "LicorpExportPlus.addin") -Value $manifest -Encoding UTF8
}

$zipPath = Join-Path $artifactRoot ("LicorpExportPlus_Setup_{0}.zip" -f $Version)
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -LiteralPath $stagingRoot -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Release folder: $versionReleaseRoot" -ForegroundColor Green
Write-Host "Staging: $stagingRoot" -ForegroundColor Green
Write-Host "Zip: $zipPath" -ForegroundColor Green
