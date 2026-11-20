param (
    [Parameter(Mandatory = $true)]
    [string]$PathToTestPackageContent
)

$ErrorActionPreference = 'Stop'

# Import common code module
Import-Module -Name (Join-Path $PSScriptRoot 'CommonCode.psm1')

# Deploys the Service Management solution package (bundled at build time into TestPackageContent\Dependencies
# by SLC-Service-Management.csproj) so its own installer runs: SDM registration, DOM modules, scripts, apps, ...
# The regression test scripts themselves are installed by this Test Package.
try {
    Write-Host 'Deploying the Service Management solution package...' -ForegroundColor Cyan

    $pathToDependencies = Join-Path $PathToTestPackageContent 'Dependencies'
    $dmapp = Get-ChildItem -Path $pathToDependencies -Filter 'SLC-Service-Management.*.dmapp' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $dmapp) {
        throw "No SLC-Service-Management.*.dmapp found in '$pathToDependencies'. Build SLC-Service-Management before the Test Package."
    }

    dotnet tool update Skyline.DataMiner.CICD.Tools.DataMinerDeploy --global --add-source https://api.nuget.org/v3/index.json | Out-Null
    dotnet tool update Skyline.DataMiner.CICD.Tools.WinEncryptedKeys --global --add-source https://api.nuget.org/v3/index.json | Out-Null
    $env:PATH += ";$(Join-Path $env:USERPROFILE '.dotnet\tools')"

    $user = (& WinEncryptedKeys --name 'QAOpsDataMinerUser' 2>&1 | Where-Object { ([string]$_).Trim() } | Select-Object -Last 1)
    $password = (& WinEncryptedKeys --name 'QAOpsDataMinerPassword' 2>&1 | Where-Object { ([string]$_).Trim() } | Select-Object -Last 1)

    if ([string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($password)) {
        throw 'Could not retrieve the QAOps DataMiner credentials.'
    }

    & dataminer-package-deploy from-artifact `
        --path-to-artifact $dmapp.FullName `
        --dm-server-location 'localhost' `
        --dm-user ([string]$user).Trim() `
        --dm-password ([string]$password).Trim() `
        --deploy-timeout-in-seconds 3600

    if ($LASTEXITCODE -ne 0) {
        throw "Deploying '$($dmapp.Name)' failed with exit code $LASTEXITCODE."
    }

    Write-Host "Deployed '$($dmapp.Name)'." -ForegroundColor Green
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
