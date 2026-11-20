$ErrorActionPreference = 'Stop'

$pathToGeneratedTests = Join-Path $PSScriptRoot 'tests.generated'
$pathToPostBuildTests = Join-Path $PSScriptRoot 'postbuild.generated'

if (Test-Path $pathToGeneratedTests) {
    Remove-Item -Recurse -Force $pathToGeneratedTests
}

New-Item -ItemType Directory -Force -Path $pathToGeneratedTests | Out-Null

if (!(Test-Path $pathToPostBuildTests)) {
    throw "Could not find harvested integration test output at '$pathToPostBuildTests'. Build SLC-Service-Management.IntegrationTests before building the Test Package."
}

# The MSTest project copies its full build output here (see CopyIntegrationTestOutputToTestPackage in its .csproj).
Copy-Item -Path (Join-Path $pathToPostBuildTests '*') -Destination $pathToGeneratedTests -Recurse -Force

$testAssembly = Get-ChildItem -Path $pathToGeneratedTests -Recurse -Filter 'SLC-Service-Management.IntegrationTests.dll' | Select-Object -First 1
if (-not $testAssembly) {
    throw "The harvested output under '$pathToPostBuildTests' does not contain SLC-Service-Management.IntegrationTests.dll."
}

Write-Host "Harvested integration tests: $($testAssembly.FullName)"

# Do not clean up the collected files here; the next SDK step packages them.
exit 0
