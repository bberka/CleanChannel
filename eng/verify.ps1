param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",
    [switch] $IncludePro,
    [string] $ProRoot = (Join-Path $PSScriptRoot "..\private\CleanChannel.Pro")
)

$ErrorActionPreference = "Stop"

# Avalonia's optional build telemetry should never be part of repository verification.
$env:AVALONIA_TELEMETRY_OPTOUT = "1"

$solutionPath = Join-Path $PSScriptRoot "..\CleanChannel.sln"
$websiteProjectPath = Join-Path $PSScriptRoot "..\website\CleanChannel.Landing\CleanChannel.Landing.csproj"
$websiteTestsProjectPath = Join-Path $PSScriptRoot "..\website\CleanChannel.Landing.Tests\CleanChannel.Landing.Tests.csproj"
$nativeManifestPath = Join-Path $PSScriptRoot "..\src\native\Cargo.toml"

dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

dotnet build $solutionPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

dotnet test $solutionPath --configuration $Configuration --no-build --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
}

dotnet restore $websiteProjectPath
if ($LASTEXITCODE -ne 0) {
    throw "website restore failed with exit code $LASTEXITCODE"
}

dotnet restore $websiteTestsProjectPath
if ($LASTEXITCODE -ne 0) {
    throw "website tests restore failed with exit code $LASTEXITCODE"
}

dotnet build $websiteProjectPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "website build failed with exit code $LASTEXITCODE"
}

dotnet build $websiteTestsProjectPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "website tests build failed with exit code $LASTEXITCODE"
}

dotnet test $websiteTestsProjectPath --configuration $Configuration --no-build --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "website tests failed with exit code $LASTEXITCODE"
}

cargo fmt --manifest-path $nativeManifestPath --check
if ($LASTEXITCODE -ne 0) {
    throw "cargo fmt --check failed with exit code $LASTEXITCODE"
}

cargo test --manifest-path $nativeManifestPath
if ($LASTEXITCODE -ne 0) {
    throw "cargo test failed with exit code $LASTEXITCODE"
}

cargo build --manifest-path $nativeManifestPath
if ($LASTEXITCODE -ne 0) {
    throw "cargo build failed with exit code $LASTEXITCODE"
}

if ($IncludePro) {
    $proSolutionPath = Join-Path $ProRoot "CleanChannel.Pro.sln"

    if (-not (Test-Path -LiteralPath $proSolutionPath -PathType Leaf)) {
        throw "-IncludePro was specified, but the private Pro solution was not found at '$proSolutionPath'."
    }

    $proHostProjectPath = Join-Path $ProRoot "CleanChannel.ProHost\CleanChannel.ProHost.csproj"
    if (-not (Test-Path -LiteralPath $proHostProjectPath -PathType Leaf)) {
        throw "-IncludePro was specified, but the expected ProHost project was not found at '$proHostProjectPath'."
    }

    $proHostProject = Get-Content -LiteralPath $proHostProjectPath -Raw
    if ($proHostProject -notmatch "CleanChannel\.ProContracts") {
        throw "The private ProHost project must reference the public CleanChannel.ProContracts project or package."
    }

    dotnet restore $proSolutionPath /p:CleanChannelProEnabled=true /p:CleanChannelProRoot=$ProRoot
    if ($LASTEXITCODE -ne 0) {
        throw "private Pro restore failed with exit code $LASTEXITCODE"
    }

    dotnet build $proSolutionPath --configuration $Configuration --no-restore /p:CleanChannelProEnabled=true /p:CleanChannelProRoot=$ProRoot
    if ($LASTEXITCODE -ne 0) {
        throw "private Pro build failed with exit code $LASTEXITCODE"
    }

    dotnet test $proSolutionPath --configuration $Configuration --no-build --no-restore /p:CleanChannelProEnabled=true /p:CleanChannelProRoot=$ProRoot
    if ($LASTEXITCODE -ne 0) {
        throw "private Pro tests failed with exit code $LASTEXITCODE"
    }
}

Write-Output "CleanChannel verification passed."
