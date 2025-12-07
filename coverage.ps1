# coverage.ps1 - runs tests and generates coverage report
$SolutionPath = "$PSScriptRoot\bottle-tycoon-microservice.sln"
$Configuration = "Release"
$OutputDir = "$PSScriptRoot\coverage"

function Exec($cmd) {
    Write-Host "RUN: $cmd"
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) { throw "Command failed: $cmd" }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet CLI not found in PATH"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Detect whether any test project references coverlet.collector
$csprojFiles = Get-ChildItem -Path "$PSScriptRoot\src" -Recurse -Filter *.csproj -ErrorAction SilentlyContinue
$hasCoverletCollector = $false
foreach ($f in $csprojFiles) {
    if (Select-String -Path $f.FullName -Pattern "coverlet.collector" -SimpleMatch -Quiet) { $hasCoverletCollector = $true; break }
}

if ($hasCoverletCollector) {
    $dotnetTest = "dotnet test `"$SolutionPath`" --configuration $Configuration --collect:`"XPlat Code Coverage`""
}
else {
    $coverletOutput = Join-Path $OutputDir "coverage.opencover.xml"
    $coverletProps = "/p:CollectCoverage=true /p:CoverletOutput=`"$coverletOutput`" /p:CoverletOutputFormat=opencover"
    $dotnetTest = "dotnet test `"$SolutionPath`" --configuration $Configuration $coverletProps"
}

$dotnetRestore = "dotnet restore `"$SolutionPath`""
Exec $dotnetRestore

Exec $dotnetTest

# Collect possible report files from both approaches
$reports = @()
$reports += Get-ChildItem -Path $OutputDir -Recurse -Filter *.opencover.xml -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
$reports += Get-ChildItem -Path $PSScriptRoot -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
$reports = $reports | Select-Object -Unique

if (-not $reports -or $reports.Count -eq 0) {
    Write-Host "No coverage reports found in $OutputDir or TestResults directories"
    exit 0
}

$reportsArg = $reports -join ";"

# Ensure reportgenerator is installed (install global tool if missing)
if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    Write-Host "reportgenerator not found. Installing dotnet-reportgenerator-globaltool (global tool)..."
    Invoke-Expression "dotnet tool install -g dotnet-reportgenerator-globaltool"
    $dotnetTools = Join-Path $env:USERPROFILE ".dotnet\tools"
    if (Test-Path $dotnetTools) { $env:PATH = "$dotnetTools;$env:PATH" }
}

if (Get-Command reportgenerator -ErrorAction SilentlyContinue) {
    $rgCmd = "reportgenerator -reports:`"$reportsArg`" -targetdir:`"$($OutputDir)\report`" -reporttypes:Html"
    Exec $rgCmd
    Write-Host "HTML report: $OutputDir\report\index.html"

    # Open the HTML report when running interactively; skip in CI
    $reportIndex = Join-Path $OutputDir 'report\index.html'
    $reportIndexAlt = Join-Path $OutputDir 'report\index.htm'

    function Open-InBrowser($path) {
        $attempt = 0
        while ($attempt -lt 3) {
            $attempt++
            Write-Host "Attempt $attempt to open $path"
            try {
                Start-Process -FilePath $path -Verb Open -ErrorAction Stop
                Write-Host "Opened $path with Start-Process -Verb Open."
                return $true
            } catch {
            }
            try {
                Start-Process -FilePath $path -ErrorAction Stop
                Write-Host "Opened $path with Start-Process."
                return $true
            } catch {
            }
            try {
                Start-Process -FilePath "explorer.exe" -ArgumentList $path -ErrorAction Stop
                Write-Host "Opened $path with explorer.exe."
                return $true
            } catch {
            }
            try {
                Invoke-Item -Path $path -ErrorAction Stop
                Write-Host "Opened $path with Invoke-Item."
                return $true
            } catch {
            }
            try {
                Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "start", "", $path -ErrorAction Stop
                Write-Host "Opened $path with cmd start."
                return $true
            } catch {
            }
            try {
                $psi = New-Object System.Diagnostics.ProcessStartInfo
                $psi.FileName = $path
                $psi.UseShellExecute = $true
                [System.Diagnostics.Process]::Start($psi) | Out-Null
                Write-Host "Opened $path with ProcessStartInfo (UseShellExecute=true)."
                return $true
            } catch {
            }
            Start-Sleep -Seconds 1
        }
        return $false
    }

    if (-not ($env:CI -or $env:GITHUB_ACTIONS)) {
        if (Test-Path $reportIndex) {
            if (-not (Open-InBrowser $reportIndex)) { Write-Host "Failed to open $reportIndex with all methods." }
        }
        elseif (Test-Path $reportIndexAlt) {
            if (-not (Open-InBrowser $reportIndexAlt)) { Write-Host "Failed to open $reportIndexAlt with all methods." }
        }
        else {
            Write-Host "Report generated but index file not found to open."
        }
    }
    else {
        Write-Host "CI environment detected; skipping opening the HTML report."
    }
}
else {
    Write-Host "reportgenerator not available. Coverage XML files:"
    $reports | ForEach-Object { Write-Host " - $_" }
    Write-Host "To generate HTML locally: dotnet tool install -g dotnet-reportgenerator-globaltool; reportgenerator -reports:`"$reportsArg`" -targetdir:`"$OutputDir\report`" -reporttypes:Html"
    exit 0
}

Write-Host "Coverage report files:"
$reports | ForEach-Object { Write-Host " - $_" }

Write-Host "Done."