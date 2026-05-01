[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipDeploy,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceRoot = Join-Path $ProjectRoot "Source"
$ArtifactsRoot = Join-Path $ProjectRoot "artifacts"
$ReleaseArtifactsRoot = Join-Path $ArtifactsRoot "release"
$BundleRoot = Join-Path $ReleaseArtifactsRoot "LicorpExportPlus.bundle"
$BundleContents = Join-Path $BundleRoot "Contents"
$ProgramDataBundle = "C:\ProgramData\Autodesk\ApplicationPlugins\LicorpExportPlus.bundle"
$BuildPropsPath = Join-Path $ProjectRoot "Directory.Build.props"

[xml]$buildProps = Get-Content -LiteralPath $BuildPropsPath
$buildMetadata = $buildProps.Project.PropertyGroup
$AddInId = $buildMetadata.AddInId
$VendorId = $buildMetadata.VendorId
$VendorDescription = $buildMetadata.VendorDescription
$Company = $buildMetadata.Company
$Version = $buildMetadata.Version

function Write-Step([string]$message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

function New-CleanDirectory([string]$Path) {
    if ([System.IO.Directory]::Exists($Path)) {
        $deleted = $false
        for ($attempt = 1; $attempt -le 5 -and -not $deleted; $attempt++) {
            try {
                Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
                $deleted = $true
            }
            catch {
                if ($attempt -eq 5) {
                    $stalePath = "$Path.stale.$(Get-Date -Format 'yyyyMMddHHmmss')"
                    try {
                        Rename-Item -LiteralPath $Path -NewName (Split-Path -Leaf $stalePath) -ErrorAction Stop
                        Write-Warning "Could not delete old directory, renamed it to: $stalePath"
                        $deleted = $true
                    }
                    catch {
                        throw "Cannot clean directory after $attempt attempts: $Path. $($_.Exception.Message)"
                    }
                }

                Start-Sleep -Milliseconds (250 * $attempt)
            }
        }
    }

    [System.IO.Directory]::CreateDirectory($Path) | Out-Null
}

function Copy-FolderContents([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Source folder not found: $Source"
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Test-RevitIsRunning {
    return [bool](Get-Process -Name "Revit" -ErrorAction SilentlyContinue)
}

function Assert-BundleLayout([string]$RootPath) {
    $packagePath = Join-Path $RootPath "PackageContents.xml"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "PackageContents.xml not found: $packagePath"
    }

    foreach ($label in @("R2020", "R2025", "R2027")) {
        $contentDir = Join-Path $RootPath "Contents\$label"
        $addinPath = Join-Path $contentDir "LicorpExportPlus.addin"
        $assemblyPath = Join-Path $contentDir "LicorpExportPlus.dll"

        if (-not (Test-Path -LiteralPath $contentDir)) {
            throw "Bundle content folder not found: $contentDir"
        }

        if (-not (Test-Path -LiteralPath $addinPath)) {
            throw "Bundle addin manifest not found: $addinPath"
        }

        if (-not (Test-Path -LiteralPath $assemblyPath)) {
            throw "Bundle assembly not found: $assemblyPath"
        }
    }
}

function Assert-LocalManifest([string]$Year) {
    $addinPath = Join-Path $env:ProgramData "Autodesk\Revit\Addins\$Year\LicorpExportPlus.addin"
    if (-not (Test-Path -LiteralPath $addinPath)) {
        throw "Local Revit manifest not found: $addinPath"
    }
}

function Get-ManifestContent([string]$AssemblyPath) {
@"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>LicorpExportPlus</Name>
    <Assembly>$AssemblyPath</Assembly>
    <AddInId>$AddInId</AddInId>
    <FullClassName>LicorpExportPlus.ExportPlusApplication</FullClassName>
    <VendorId>$VendorId</VendorId>
    <VendorDescription>$VendorDescription</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
}

function New-BundleManifest([string]$TargetPath) {
    Set-Content -LiteralPath $TargetPath -Value (Get-ManifestContent -AssemblyPath ".\LicorpExportPlus.dll") -Encoding UTF8
}

function New-LocalManifest([string]$Year, [string]$AssemblyPath) {
    $addinDir = Join-Path $env:ProgramData "Autodesk\Revit\Addins\$Year"
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
    $addinPath = Join-Path $addinDir "LicorpExportPlus.addin"
    Set-Content -LiteralPath $addinPath -Value (Get-ManifestContent -AssemblyPath $AssemblyPath) -Encoding UTF8
}

function New-PackageContentsXml([string]$TargetPath) {
@"
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage SchemaVersion="1.0"
Name="Licorp Export+"
Description="Licorp Export+ - Professional Batch Export for Revit"
Author="$Company"
AppVersion="$Version"
ProductCode="{$AddInId}"
ProductType="Application">
  <Company Name="$Company" />

  <Components>
    <ComponentEntry AppName="Licorp Export+"
                    Version="$Version"
                    ModuleName="./Contents/R2020/LicorpExportPlus.addin"
                    AppDescription="Professional Batch Export for Revit"
                    LoadOnRevitStartup="True">
      <RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R2020" SeriesMax="R2024" />
    </ComponentEntry>
  </Components>

  <Components>
    <ComponentEntry AppName="Licorp Export+"
                    Version="$Version"
                    ModuleName="./Contents/R2025/LicorpExportPlus.addin"
                    AppDescription="Professional Batch Export for Revit"
                    LoadOnRevitStartup="True">
      <RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R2025" SeriesMax="R2026" />
    </ComponentEntry>
  </Components>

  <Components>
    <ComponentEntry AppName="Licorp Export+"
                    Version="$Version"
                    ModuleName="./Contents/R2027/LicorpExportPlus.addin"
                    AppDescription="Professional Batch Export for Revit"
                    LoadOnRevitStartup="True">
      <RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R2027" SeriesMax="R2027" />
    </ComponentEntry>
  </Components>
</ApplicationPackage>
"@ | Set-Content -LiteralPath $TargetPath -Encoding UTF8
}

$projects = @(
    @{ Label = "R2020"; Path = Join-Path $SourceRoot "LicorpExportPlus.R20\LicorpExportPlus.R20.csproj"; Output = Join-Path $ProjectRoot "bin\R2020\$Configuration" },
    @{ Label = "R2025"; Path = Join-Path $SourceRoot "LicorpExportPlus.R25\LicorpExportPlus.R25.csproj"; Output = Join-Path $ProjectRoot "bin\R2025\$Configuration" },
    @{ Label = "R2027"; Path = Join-Path $SourceRoot "LicorpExportPlus.R27\LicorpExportPlus.R27.csproj"; Output = Join-Path $ProjectRoot "bin\R2027\$Configuration" }
)

if (-not $SkipBuild) {
    Write-Step "Building projects"
    foreach ($project in $projects) {
        Write-Host "Building $($project.Label)..." -ForegroundColor DarkGray
        & dotnet build $project.Path -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed: $($project.Label)"
        }
    }
}

Write-Step "Preparing bundle layout"
New-CleanDirectory $BundleRoot
[System.IO.Directory]::CreateDirectory($BundleContents) | Out-Null

foreach ($project in $projects) {
    $targetDir = Join-Path $BundleContents $project.Label
    Copy-FolderContents $project.Output $targetDir
    New-BundleManifest (Join-Path $targetDir "LicorpExportPlus.addin")
}

New-PackageContentsXml (Join-Path $BundleRoot "PackageContents.xml")
Assert-BundleLayout $BundleRoot

if ($SkipDeploy) {
    Write-Step "Deploy skipped"
    Write-Host "Bundle prepared and verified at:" -ForegroundColor Green
    Write-Host "  $BundleRoot"
    return
}

Write-Step "Deploying bundle to ProgramData"
if (Test-RevitIsRunning) {
    throw "Revit is running. Close Revit before deploying so LicorpExportPlus.dll is not locked."
}

New-CleanDirectory $ProgramDataBundle
Copy-FolderContents $BundleRoot $ProgramDataBundle
Assert-BundleLayout $ProgramDataBundle

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

Write-Step "Writing local Revit manifests"
foreach ($year in $yearMap.Keys) {
    $assemblyPath = Join-Path $ProgramDataBundle ("Contents\" + $yearMap[$year] + "\LicorpExportPlus.dll")
    New-LocalManifest -Year $year -AssemblyPath $assemblyPath
    Assert-LocalManifest -Year $year
}

Write-Step "Done"
Write-Host "Bundle prepared at:" -ForegroundColor Green
Write-Host "  $BundleRoot"
Write-Host "Installed bundle:" -ForegroundColor Green
Write-Host "  $ProgramDataBundle"
