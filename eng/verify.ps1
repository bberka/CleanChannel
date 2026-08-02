$ErrorActionPreference = "Stop"

# Avalonia's optional build telemetry should never be part of repository verification.
$env:AVALONIA_TELEMETRY_OPTOUT = "1"

$solutionPath = Join-Path $PSScriptRoot "..\CleanChannel.sln"
$nativeManifestPath = Join-Path $PSScriptRoot "..\src\native\Cargo.toml"

dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

dotnet build $solutionPath --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

dotnet test $solutionPath --configuration Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
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

Write-Output "CleanChannel verification passed."
