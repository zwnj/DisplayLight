[CmdletBinding()]
param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-LastExitCode {
    param([Parameter(Mandatory)][string]$Operation)

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation に失敗しました。終了コード: $LASTEXITCODE"
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    if (-not $NoRestore) {
        dotnet restore DisplayLight.slnx --locked-mode
        Assert-LastExitCode 'dotnet restore'
    }

    dotnet format DisplayLight.slnx --verify-no-changes --no-restore
    Assert-LastExitCode 'dotnet format'

    dotnet build DisplayLight.slnx --configuration Release --no-restore
    Assert-LastExitCode 'dotnet build'

    dotnet test DisplayLight.slnx --configuration Release --no-build --no-restore
    Assert-LastExitCode 'dotnet test'
}
finally {
    Pop-Location
}
