param (
    [Parameter(Mandatory = $true)]
    [string]$PathToTestPackageContent
)

$ErrorActionPreference = 'Stop'

# Import common code module
Import-Module -Name (Join-Path $PSScriptRoot 'CommonCode.psm1')

Install-Module Skyline.DataMiner.QAOps.PipelineLibrary -Repository PSGallery -Force -Scope CurrentUser -MinimumVersion 1.3.0
Import-Module Skyline.DataMiner.QAOps.PipelineLibrary -Force

$scriptStart = Get-Date

$pathToTestHarvesting = Join-Path $PathToTestPackageContent 'TestHarvesting'
$pathToGeneratedTests = Join-Path $pathToTestHarvesting 'tests.generated'
$testAssemblyPath = Join-Path $pathToGeneratedTests 'SLC_SM_RT.QAOps.IntegrationTests\SLC_SM_RT.QAOps.IntegrationTests.dll'
$testCaseName = 'pipeline_2.ExecuteServiceManagementRegressionTests'

try {
    Write-Host 'Running the Service Management regression tests...' -ForegroundColor Cyan

    # A failing test is not a pipeline failure: the helper publishes one row per test (failures included).
    # For a scoped development run append, e.g.:
    #   -TestFilter 'TestCategory=ServiceOrder' -PublishNotExecuted $false
    # and restore the unfiltered call before a release-validation run.
    Invoke-DotNetTestAndPublishResults `
        -PathToTestPackageContent $PathToTestPackageContent `
        -TestDllPath $testAssemblyPath `
        -ResultsFileName 'servicemanagement-regression.trx'

    try {
        Push-TestCaseResult -Outcome 'OK' -Name $testCaseName -Duration ((Get-Date) - $scriptStart) -Message 'Test Package execution finished.' -TestAspect Execution
    }
    catch {
        Write-Host "Skipped Push for OK on $testCaseName"
    }
}
catch {
    try {
        Push-TestCaseResult -Outcome 'Fail' -Name $testCaseName -Duration ((Get-Date) - $scriptStart) -Message (Limit-String "Exception during Test Package execution: $($_.Exception.Message)" 1000) -TestAspect Execution
    }
    catch {
        Write-Host "Skipped Push for Fail on $testCaseName"
    }

    exit 1
}
