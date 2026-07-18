[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw '.NET SDK の確認に失敗しました。'
    }

    dotnet restore DisplayLight.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw 'NuGet パッケージの復元に失敗しました。'
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'ローカルツールの復元に失敗しました。'
    }
}
finally {
    Pop-Location
}
