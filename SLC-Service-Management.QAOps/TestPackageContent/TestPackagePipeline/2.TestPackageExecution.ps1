param (
    [Parameter(Mandatory = $true)]
    [string]$PathToTestPackageContent
)

$ErrorActionPreference = 'Stop'

Import-Module -Name (Join-Path $PSScriptRoot 'CommonCode.psm1')

# Invoke-DotNetTestAndPublishResults publishes one QAOps row per test, failures included. Nothing that can throw
# may run before it: a failing test is not a pipeline failure.
Install-Module Skyline.DataMiner.QAOps.PipelineLibrary -Repository PSGallery -Force -Scope CurrentUser -MinimumVersion 1.3.0
Import-Module Skyline.DataMiner.QAOps.PipelineLibrary -Force

$scriptStart = Get-Date
$testCaseName = 'pipeline_2.TestPackageExecution'

$pathToGeneratedTests = Join-Path (Join-Path $PathToTestPackageContent 'TestHarvesting') 'tests.generated'
$testAssemblyPath = Join-Path $pathToGeneratedTests 'SLC-Service-Management.IntegrationTests\SLC-Service-Management.IntegrationTests.dll'

try {
    Write-Host 'Running Service Management integration tests...' -ForegroundColor Cyan

    if (!(Test-Path $testAssemblyPath)) {
        throw "Test assembly not found at '$testAssemblyPath'."
    }

    Invoke-DotNetTestAndPublishResults `
        -PathToTestPackageContent $PathToTestPackageContent `
        -TestDllPath $testAssemblyPath `
        -ResultsFileName 'SLC-Service-Management.IntegrationTests.trx'

    try {
        Push-TestCaseResult -Outcome 'OK' -Name $testCaseName -Duration ((Get-Date) - $scriptStart) -Message 'Test Package execution finished.' -TestAspect Execution
    }
    catch {
        Write-Host "Skipped Push for OK on $testCaseName"
    }
}
catch {
    try {
        Push-TestCaseResult -Outcome 'Fail' -Name $testCaseName -Duration ((Get-Date) - $scriptStart) -Message "Exception during Test Package execution: $($_.Exception.Message)" -TestAspect Execution
    }
    catch {
        Write-Host "Skipped Push for Fail on $testCaseName"
    }

    exit 1
}
