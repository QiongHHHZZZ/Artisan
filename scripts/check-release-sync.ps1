param(
    [Parameter(Mandatory = $false)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$Owner = 'QiongHHHZZZ',

    [Parameter(Mandatory = $false)]
    [string]$Repo = 'Artisan',

    [Parameter(Mandatory = $false)]
    [string]$PluginRepoOwner = 'QiongHHHZZZ',

    [Parameter(Mandatory = $false)]
    [string]$PluginRepoName = 'DalamudPlugins'
)

$ErrorActionPreference = 'Stop'

function Get-VersionFromCsproj {
    $csprojPath = Join-Path $PSScriptRoot '..\Artisan\Artisan.csproj'
    [xml]$xml = Get-Content -Path $csprojPath -Raw

    $versionNode = $xml.Project.PropertyGroup |
        ForEach-Object { $_.Version } |
        Where-Object { $_ } |
        Select-Object -First 1

    if (-not $versionNode) {
        throw "Version node not found in $csprojPath"
    }

    return [string]$versionNode
}

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-VersionFromCsproj
}

$tag = "v$Version"
$assetName = 'Artisan.zip'
$expectedAssetUrl = "https://github.com/$Owner/$Repo/releases/download/$tag/$assetName"
$releaseApiUrl = "https://api.github.com/repos/$Owner/$Repo/releases/tags/$tag"
$pluginMasterUrl = "https://raw.githubusercontent.com/$PluginRepoOwner/$PluginRepoName/main/pluginmaster.json"

$headers = @{ 'User-Agent' = 'release-sync-check' }

Write-Host "[INFO] Version: $Version"
Write-Host "[INFO] Tag: $tag"

$release = Invoke-RestMethod -Uri $releaseApiUrl -Headers $headers -Method Get
Assert-Condition ($null -ne $release) "Release not found: $tag"

$asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
Assert-Condition ($null -ne $asset) "Release $tag missing asset: $assetName"
Assert-Condition ($asset.size -gt 0) "Release asset size is zero: $assetName"

Write-Host "[PASS] Release asset exists: $($asset.browser_download_url)"

$pluginMaster = Invoke-RestMethod -Uri $pluginMasterUrl -Headers $headers -Method Get
$artisan = $pluginMaster | Where-Object { $_.InternalName -eq 'Artisan' } | Select-Object -First 1

Assert-Condition ($null -ne $artisan) "Artisan entry not found in pluginmaster.json"
Assert-Condition ($artisan.AssemblyVersion -eq $Version) "AssemblyVersion mismatch. Current=$($artisan.AssemblyVersion), Expected=$Version"
Assert-Condition ($artisan.DownloadLinkInstall -eq $expectedAssetUrl) "DownloadLinkInstall mismatch. Current=$($artisan.DownloadLinkInstall), Expected=$expectedAssetUrl"
Assert-Condition ($artisan.DownloadLinkUpdate -eq $expectedAssetUrl) "DownloadLinkUpdate mismatch. Current=$($artisan.DownloadLinkUpdate), Expected=$expectedAssetUrl"

Write-Host "[PASS] pluginmaster version and download links match"
Write-Host "[INFO] All release sync checks passed"
