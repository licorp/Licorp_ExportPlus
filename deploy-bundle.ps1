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

$runtimeProjects = @(
    @{ Label = "R2020"; Versions = "2020-2024"; Path = Join-Path $SourceRoot "LicorpExportPlus.R20\LicorpExportPlus.R20.csproj"; Output = Join-Path $ProjectRoot "bin\R2020\$Configuration" },
    @{ Label = "R2025"; Versions = "2025-2026"; Path = Join-Path $SourceRoot "LicorpExportPlus.R25\LicorpExportPlus.R25.csproj"; Output = Join-Path $ProjectRoot "bin\R2025\$Configuration" },
    @{ Label = "R2027"; Versions = "2027"; Path = Join-Path $SourceRoot "LicorpExportPlus.R27\LicorpExportPlus.R27.csproj"; Output = Join-Path $ProjectRoot "bin\R2027\$Configuration" }
)

$revitDeployments = @(
    @{ Year = "2020"; Label = "R2020"; RuntimeLabel = "R2020" },
    @{ Year = "2021"; Label = "R2021"; RuntimeLabel = "R2020" },
    @{ Year = "2022"; Label = "R2022"; RuntimeLabel = "R2020" },
    @{ Year = "2023"; Label = "R2023"; RuntimeLabel = "R2020" },
    @{ Year = "2024"; Label = "R2024"; RuntimeLabel = "R2020" },
    @{ Year = "2025"; Label = "R2025"; RuntimeLabel = "R2025" },
    @{ Year = "2026"; Label = "R2026"; RuntimeLabel = "R2025" },
    @{ Year = "2027"; Label = "R2027"; RuntimeLabel = "R2027" }
)

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
    Get-ChildItem -LiteralPath $Source -Force | Where-Object {
        $_.Name -notin @("publish", "obj")
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Test-RevitIsRunning {
    return [bool](Get-Process -Name "Revit" -ErrorAction SilentlyContinue)
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-BundleLayout([string]$RootPath) {
    $packagePath = Join-Path $RootPath "PackageContents.xml"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "PackageContents.xml not found: $packagePath"
    }

    foreach ($deployment in $revitDeployments) {
        $label = $deployment.Label
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
    $addinPath = Get-RevitManifestPath -Year $Year
    if (-not (Test-Path -LiteralPath $addinPath)) {
        throw "Revit manifest not found: $addinPath"
    }
}

function Assert-LocalManifestAssembly([string]$Year, [string]$ExpectedAssemblyPath) {
    $addinPath = Get-RevitManifestPath -Year $Year
    Assert-LocalManifest -Year $Year

    [xml]$addinXml = Get-Content -LiteralPath $addinPath
    $actualAssemblyPath = $addinXml.RevitAddIns.AddIn.Assembly
    if ($actualAssemblyPath -ne $ExpectedAssemblyPath) {
        throw "Manifest assembly mismatch for Revit $Year. Expected '$ExpectedAssemblyPath' but found '$actualAssemblyPath'."
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
    $addinPath = Get-RevitManifestPath -Year $Year
    $addinDir = Split-Path -Parent $addinPath
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
    Set-Content -LiteralPath $addinPath -Value (Get-ManifestContent -AssemblyPath $AssemblyPath) -Encoding UTF8
}

function Get-RevitManifestPath([string]$Year) {
    return Join-Path $env:ProgramData "Autodesk\Revit\Addins\$Year\LicorpExportPlus.addin"
}

function Remove-StaleManifest([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
        Write-Host "Removed stale manifest: $Path" -ForegroundColor DarkGray
    }
}

function Remove-StaleRevitManifests([string]$Year) {
    $targetPath = Get-RevitManifestPath -Year $Year
    $candidatePaths = @(
        (Join-Path $env:APPDATA "Autodesk\Revit\Addins\$Year\LicorpExportPlus.addin"),
        (Join-Path $env:ProgramData "Autodesk\Revit\Addins\$Year\LicorpExportPlus.addin"),
        (Join-Path ${env:ProgramFiles} "Autodesk\Revit $Year\AddIns\LicorpExportPlus.addin"),
        (Join-Path ${env:ProgramFiles} "Autodesk\Revit $Year\AddIns\LicorpExportPlus\LicorpExportPlus.addin")
    ) | Select-Object -Unique

    foreach ($path in $candidatePaths) {
        if ($path -ne $targetPath) {
            Remove-StaleManifest -Path $path
        }
    }
}

function New-PackageContentsXml([string]$TargetPath) {
    $components = foreach ($deployment in $revitDeployments) {
@"
    <ComponentEntry AppName="Licorp Export+"
                    Version="$Version"
                    ModuleName="./Contents/$($deployment.Label)/LicorpExportPlus.addin"
                    AppDescription="Professional Batch Export for Revit"
                    LoadOnRevitStartup="True">
      <RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R$($deployment.Year)" SeriesMax="R$($deployment.Year)" />
    </ComponentEntry>
"@
    }

    $componentsText = $components -join "`r`n`r`n"

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
$componentsText
  </Components>
</ApplicationPackage>
"@ | Set-Content -LiteralPath $TargetPath -Encoding UTF8
}

if (-not $SkipBuild) {
    Write-Step "Building runtime projects"
    foreach ($project in $runtimeProjects) {
        Write-Host "Building $($project.Label) runtime for Revit $($project.Versions)..." -ForegroundColor DarkGray
        & dotnet build $project.Path -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed: $($project.Label)"
        }
    }

    Write-Host ""
    Write-Host "Runtime deployment map:" -ForegroundColor DarkGray
    foreach ($deployment in $revitDeployments) {
        Write-Host "  Revit $($deployment.Year) -> Contents\$($deployment.Label) copied from $($deployment.RuntimeLabel)" -ForegroundColor DarkGray
    }
}

Write-Step "Preparing bundle layout"
New-CleanDirectory $BundleRoot
[System.IO.Directory]::CreateDirectory($BundleContents) | Out-Null

foreach ($deployment in $revitDeployments) {
    $runtimeProject = $runtimeProjects | Where-Object { $_.Label -eq $deployment.RuntimeLabel } | Select-Object -First 1
    if ($null -eq $runtimeProject) {
        throw "Runtime project not found for deployment $($deployment.Label): $($deployment.RuntimeLabel)"
    }

    $targetDir = Join-Path $BundleContents $deployment.Label
    Write-Host "Preparing $($deployment.Label) from $($deployment.RuntimeLabel) runtime..." -ForegroundColor DarkGray
    Copy-FolderContents $runtimeProject.Output $targetDir
    New-BundleManifest (Join-Path $targetDir "LicorpExportPlus.addin")
}

New-PackageContentsXml (Join-Path $BundleRoot "PackageContents.xml")
Assert-BundleLayout $BundleRoot

if ($SkipDeploy) {
    Write-Step "Deploy skipped"
    Write-Host "Bundle prepared and verified at:" -ForegroundColor Green
    Write-Host "  $BundleRoot"
    Write-Host "Bundle version folders:" -ForegroundColor Green
    foreach ($deployment in $revitDeployments) {
        Write-Host "  Contents\$($deployment.Label) -> Revit $($deployment.Year)"
    }
    return
}

Write-Step "Deploying bundle to ProgramData"
if (Test-RevitIsRunning) {
    throw "Revit is running. Close Revit before deploying so LicorpExportPlus.dll is not locked."
}

New-CleanDirectory $ProgramDataBundle
Copy-FolderContents $BundleRoot $ProgramDataBundle
Assert-BundleLayout $ProgramDataBundle

Write-Step "Writing local Revit manifests"
foreach ($deployment in $revitDeployments) {
    $year = $deployment.Year
    $assemblyPath = Join-Path $ProgramDataBundle ("Contents\" + $deployment.Label + "\LicorpExportPlus.dll")

    Remove-StaleRevitManifests -Year $year
    New-LocalManifest -Year $year -AssemblyPath $assemblyPath
    Assert-LocalManifestAssembly -Year $year -ExpectedAssemblyPath $assemblyPath
}

Write-Step "Done"
Write-Host "Bundle prepared at:" -ForegroundColor Green
Write-Host "  $BundleRoot"
Write-Host "Installed bundle:" -ForegroundColor Green
Write-Host "  $ProgramDataBundle"
Write-Host "Revit manifests:" -ForegroundColor Green
Write-Host "  2020-2027: $env:ProgramData\Autodesk\Revit\Addins\<year>\LicorpExportPlus.addin"
Write-Host "Bundle version folders:" -ForegroundColor Green
foreach ($deployment in $revitDeployments) {
    Write-Host "  Contents\$($deployment.Label) -> Revit $($deployment.Year)"
}
