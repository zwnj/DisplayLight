[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$NoRestore,

    [switch]$KeepReleaseDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-LastExitCode {
    param([Parameter(Mandatory)][string]$Operation)

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation に失敗しました。終了コード: $LASTEXITCODE"
    }
}

function Remove-PackagingDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ArtifactsRoot
    )

    $resolvedArtifactsRoot = [System.IO.Path]::GetFullPath($ArtifactsRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $relativePath = [System.IO.Path]::GetRelativePath($resolvedArtifactsRoot, $resolvedPath)

    if ($relativePath.StartsWith('..', [System.StringComparison]::Ordinal) -or
        [System.IO.Path]::IsPathRooted($relativePath)) {
        throw "成果物ディレクトリ外は削除できません: $resolvedPath"
    }

    if (Test-Path -LiteralPath $resolvedPath) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot "publish\DisplayLight-$Version-win-x64"
$releaseDirectory = Join-Path $artifactsRoot 'release'

Push-Location $repositoryRoot

try {
    Remove-PackagingDirectory -Path $publishDirectory -ArtifactsRoot $artifactsRoot
    if (-not $KeepReleaseDirectory) {
        Remove-PackagingDirectory -Path $releaseDirectory -ArtifactsRoot $artifactsRoot
    }

    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

    if (-not $NoRestore) {
        dotnet restore ./src/DisplayLight.App/DisplayLight.App.csproj --runtime win-x64 --locked-mode
        Assert-LastExitCode 'dotnet restore'
        dotnet tool restore
        Assert-LastExitCode 'dotnet tool restore'
    }

    dotnet publish ./src/DisplayLight.App/DisplayLight.App.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $publishDirectory `
        -p:Version=$Version `
        -p:AssemblyVersion="$Version.0" `
        -p:FileVersion="$Version.0" `
        -p:InformationalVersion=$Version `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugSymbols=false `
        -p:DebugType=None
    Assert-LastExitCode 'dotnet publish'

    dotnet vpk pack `
        --packId DisplayLight `
        --packVersion $Version `
        --packDir $publishDirectory `
        --mainExe DisplayLight.App.exe `
        --packAuthors zwnj `
        --packTitle DisplayLight `
        --runtime win-x64 `
        --shortcuts StartMenuRoot `
        --outputDir $releaseDirectory
    Assert-LastExitCode 'vpk pack'

    Write-Output "Velopack releases: $releaseDirectory"
}
finally {
    Pop-Location
}
