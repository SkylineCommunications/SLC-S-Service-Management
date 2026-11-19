$ErrorActionPreference = 'Stop'

# Collects the integration-test output that the MSTest project copied into postbuild.generated
# (see the CopyIntegrationTestOutputToTestPackage target) into tests.generated, which the SDK packages.
$pathToGeneratedTests = Join-Path $PSScriptRoot 'tests.generated'
$pathToPostBuildTests = Join-Path $PSScriptRoot 'postbuild.generated'

if (Test-Path $pathToGeneratedTests) {
    Remove-Item -Recurse -Force $pathToGeneratedTests
}

New-Item -ItemType Directory -Force -Path $pathToGeneratedTests | Out-Null

if (!(Test-Path $pathToPostBuildTests)) {
    throw "Could not find harvested integration test output at '$pathToPostBuildTests'. Build SLC_SM_RT.QAOps.IntegrationTests before building the Test Package."
}

Copy-Item -Path (Join-Path $pathToPostBuildTests '*') -Destination $pathToGeneratedTests -Recurse -Force

# Do not clean up the collected files here; the next SDK step packages them.
exit 0
