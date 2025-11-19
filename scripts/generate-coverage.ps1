# Generates test coverage report for the solution (PowerShell)
# - Runs `dotnet test` with XPlat Code Coverage
# - Installs dotnet-reportgenerator-globaltool if missing
# - Runs reportgenerator to create HTML + Cobertura
# - Prints path to HTML report and computed coverage percentage

param(
    [string]$Solution = 'bottle-tycoon-microservice.sln',
    [string]$ResultsDir = 'TestResults',
    [string]$ReportDir = 'coverage-report',
    [int]$Threshold = 0
)

Set-StrictMode -Version Latest

Write-Host "Cleaning previous artifacts..."
Remove-Item -Recurse -Force $ResultsDir, $ReportDir -ErrorAction SilentlyContinue

Write-Host "Running tests with XPlat Code Coverage..."
$dotnetTestArgs = @('--configuration','Release','--collect:XPlat Code Coverage','--results-directory', $ResultsDir, $Solution)
# run dotnet test
$testResult = dotnet test @dotnetTestArgs
Write-Host $testResult

Write-Host "Ensuring reportgenerator is installed..."
if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.1.23
} else {
    Write-Host "reportgenerator already installed"
}

# Ensure user dotnet tools folder is on PATH for this process
$toolsPath = Join-Path $env:USERPROFILE '.dotnet\tools'
$env:Path = $env:Path + ';' + $toolsPath

Write-Host "Generating coverage reports (HTML + Cobertura)..."
# Use stop-parsing --% to pass the semicolon in report types safely
& reportgenerator --% -reports:TestResults/**/coverage.cobertura.xml -targetdir:$ReportDir -reporttypes:Html;Cobertura

# Find cobertura xml
$cov = Get-ChildItem -Path $ResultsDir -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
if ($null -eq $cov) {
    Write-Error "No cobertura coverage file found under '$ResultsDir'"
    exit 2
}

Write-Host "Using coverage file: $($cov.FullName)"
[xml]$xml = Get-Content $cov.FullName
$lineRate = $xml.DocumentElement.GetAttribute('line-rate')
Write-Host "line-rate raw: $lineRate"
$percent = [math]::Round([double]$lineRate * 100)
Write-Host "Computed coverage: $percent%"

if ($Threshold -gt 0) {
    if ($percent -lt $Threshold) {
        Write-Error "Coverage $percent% is below threshold $Threshold%"
        exit 3
    } else {
        Write-Host "Coverage threshold met ($percent% >= $Threshold%)"
    }
}

$index = Join-Path $ReportDir 'index.html'
if (Test-Path $index) {
    Write-Host "HTML report: $(Resolve-Path $index)"
} else {
    Write-Host "HTML report not found at $index"
}