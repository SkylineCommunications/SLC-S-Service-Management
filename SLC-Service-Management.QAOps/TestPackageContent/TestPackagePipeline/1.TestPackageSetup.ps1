param (
    [Parameter(Mandatory = $true)]
    [string]$PathToTestPackageContent
)

$ErrorActionPreference = 'Stop'

Import-Module -Name (Join-Path $PSScriptRoot 'CommonCode.psm1')

# Deploys the Service Management solution (.dmapp bundled by the SLC-Service-Management build) on the QAOps
# DataMiner so the integration tests run against the real installer output: DOM, scripts, API and web app.
try {
    Write-Host 'Running Test Package setup...' -ForegroundColor Cyan

    $dependencies = Join-Path $PathToTestPackageContent 'Dependencies'
    $dmapp = Get-ChildItem -Path $dependencies -Filter 'SLC-Service-Management.*.dmapp' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dmapp) {
        throw "No SLC-Service-Management.*.dmapp found in '$dependencies'. The Test Package was built without the solution package."
    }

    Write-Host "Installing deployment tools..."
    $nuget = 'https://api.nuget.org/v3/index.json'
    dotnet tool update Skyline.DataMiner.CICD.Tools.DataMinerDeploy --global --add-source $nuget | Out-Host
    dotnet tool update Skyline.DataMiner.CICD.Tools.WinEncryptedKeys --global --add-source $nuget | Out-Host
    $env:PATH += ";$(Join-Path $env:USERPROFILE '.dotnet\tools')"

    $user = (& WinEncryptedKeys --name 'QAOpsDataMinerUser' 2>&1 | Where-Object { ([string]$_).Trim() } | Select-Object -Last 1).Trim()
    $password = (& WinEncryptedKeys --name 'QAOpsDataMinerPassword' 2>&1 | Where-Object { ([string]$_).Trim() } | Select-Object -Last 1).Trim()
    if (-not $user -or -not $password) {
        throw 'Could not read the QAOps DataMiner credentials from the encrypted keys.'
    }

    Write-Host "Deploying $($dmapp.Name) to localhost..."
    & dataminer-package-deploy from-artifact `
        --path-to-artifact $dmapp.FullName `
        --dm-server-location 'localhost' `
        --dm-user $user `
        --dm-password $password `
        --deploy-timeout-in-seconds 3600 | Out-Host

    if ($LASTEXITCODE -ne 0) {
        throw "dataminer-package-deploy returned exit code $LASTEXITCODE while deploying $($dmapp.Name)."
    }

    Write-Host "Deployed $($dmapp.Name)." -ForegroundColor Green
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
