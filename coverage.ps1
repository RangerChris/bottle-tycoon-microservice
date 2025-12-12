# coverage.ps1 - runs tests and generates coverage report
$SolutionPath = "$PSScriptRoot\bottle-tycoon-microservice.sln"
$Configuration = "Release"
$OutputDir = "$PSScriptRoot\coverage"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet CLI not found in PATH" }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "RUN: dotnet restore"
dotnet restore "$SolutionPath"
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

Write-Host "RUN: dotnet test (collecting XPlat Code Coverage)"
dotnet test "$SolutionPath" --configuration $Configuration --collect:"XPlat Code Coverage" --results-directory "$OutputDir\TestResults"
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }

$reports = Get-ChildItem -Path $OutputDir -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
if (-not $reports -or $reports.Count -eq 0) {
    Write-Host "No coverage reports found"
    exit 0
}
$reportsArg = $reports -join ";"

if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    Write-Host "Installing reportgenerator global tool..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
    $dotnetTools = Join-Path $env:USERPROFILE ".dotnet\tools"
    if (Test-Path $dotnetTools) { $env:PATH = "$dotnetTools;$env:PATH" }
}

$target = Join-Path $OutputDir "report"
Write-Host "RUN: reportgenerator -> $target"
reportgenerator -reports:"$reportsArg" -targetdir:"$target" -reporttypes:Html
if ($LASTEXITCODE -ne 0) { throw "reportgenerator failed" }

Write-Host "HTML report: $target\index.html"
Write-Host "Done."