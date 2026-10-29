param([switch]$SkipVerification)
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
if (!$SkipVerification) {
    & (Join-Path $repoRoot 'Verification/run.ps1') -Configuration Release
} else {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $msbuild = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.Roslyn.Compiler -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    & $msbuild (Join-Path $repoRoot 'PuregoldITToolkit/PuregoldITToolkit.csproj') /t:Build /p:Configuration=Release /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}
$appOutput = Join-Path $repoRoot 'PuregoldITToolkit/bin/Release'
$production = Join-Path $repoRoot 'PuregoldITToolkit/Production/Puregold IT Toolkit'
$installerDir = Join-Path $production 'installer'
$defaults = Join-Path $production 'defaults'
New-Item -ItemType Directory -Path $production, $installerDir, $defaults -Force | Out-Null
$version = (Get-Item (Join-Path $appOutput 'PuregoldITToolkit.exe')).VersionInfo.FileVersion.Split('.')[0..2] -join '.'
$files = @('PuregoldITToolkit.exe', 'PuregoldITToolkit.exe.config', 'WinSCP.exe', 'WinSCPnet.dll', 'StoreProfilesDatabase.json', 'license.dat')
foreach ($name in $files) {
    Copy-Item -LiteralPath (Join-Path $appOutput $name) -Destination (Join-Path $production $name) -Force
}
# Fresh install defaults are created separately from the current store's configuration.
$verification = Join-Path $repoRoot 'Verification/bin/Release/Verification.exe'
if (!(Test-Path -LiteralPath $verification)) { throw 'Run Verification/run.ps1 -Configuration Release to prepare the defaults writer.' }
& $verification --write-defaults (Join-Path $defaults 'GlobalConfig.json')
if ($LASTEXITCODE -ne 0) { throw 'Could not create install defaults.' }
# Remove the retired mailing list from existing portable settings without changing other settings.
foreach ($config in @('GlobalConfig.json', 'settings.json')) {
    $path = Join-Path $production $config
    if (!(Test-Path -LiteralPath $path)) { continue }
    $settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    $settings.Remove('InfDefaultSupportEmail') | Out-Null
    $settings.Remove('InfSupportTeamName') | Out-Null
    foreach ($field in @('InfDefaultToEmails', 'InfDefaultCcEmails')) {
        if ($settings.ContainsKey($field)) {
            $settings[$field] = ([regex]::Replace([string]$settings[$field], '(?i)(?:All IT-POS SYSTEM SUPPORT\s*)?<?allitpossystemsup@puregold\.com\.ph>?\s*[,;]?\s*', '')).Trim().Trim(',', ';')
        }
    }
    $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding utf8
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $production 'README.md') -Force
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Inno Setup 6 is required to build the installer.' }
& $compiler /Qp "/DReleaseVersion=$version" (Join-Path $installerDir 'compiler.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $installerDir "PITK_Setup_v$version.exe"
if (!(Test-Path -LiteralPath $installer)) { throw 'The expected installer was not produced.' }
$stage = Join-Path $repoRoot "PuregoldITToolkit/Production/portable-$version"
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($name in $files + @('icon.ico', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $production $name) -Destination (Join-Path $stage $name) -Force
}
Copy-Item -LiteralPath (Join-Path $defaults 'GlobalConfig.json') -Destination (Join-Path $stage 'GlobalConfig.json') -Force
New-Item -ItemType Directory -Path (Join-Path $stage 'data') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $production 'data/stores.csv') -Destination (Join-Path $stage 'data/stores.csv') -Force
$archive = Join-Path $repoRoot "PuregoldITToolkit/Production/Puregold_IT_Toolkit_v$version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
$manifest = [ordered]@{
    version = $version
    application = [ordered]@{ path = 'Puregold IT Toolkit/PuregoldITToolkit.exe'; sha256 = (Get-FileHash -LiteralPath (Join-Path $production 'PuregoldITToolkit.exe') -Algorithm SHA256).Hash }
    installer = [ordered]@{ path = "Puregold IT Toolkit/installer/PITK_Setup_v$version.exe"; sha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash }
    portable = [ordered]@{ path = "Puregold_IT_Toolkit_v$version.zip"; sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash }
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repoRoot 'PuregoldITToolkit/Production/release.json') -Encoding utf8
Write-Output "Production release $version is ready."
Write-Output "Application: $(Join-Path $production 'PuregoldITToolkit.exe')"
Write-Output "Installer: $installer"
Write-Output "Portable archive: $archive"
