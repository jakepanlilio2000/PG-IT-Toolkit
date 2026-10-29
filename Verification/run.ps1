param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.Roslyn.Compiler -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Visual Studio MSBuild is required.' }
& $msbuild (Join-Path $repoRoot 'PuregoldITToolkit/PuregoldITToolkit.csproj') /t:Build "/p:Configuration=$Configuration" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
$output = Join-Path $PSScriptRoot "bin/$Configuration"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$appOutput = Join-Path $repoRoot "PuregoldITToolkit/bin/$Configuration"
foreach ($file in @('PuregoldITToolkit.exe', 'PuregoldITToolkit.exe.config', 'WinSCPnet.dll', 'WinSCP.exe')) {
    Copy-Item -LiteralPath (Join-Path $appOutput $file) -Destination $output -Force
}
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
$compiler = Join-Path (Split-Path $msbuild) 'Roslyn/csc.exe'
# Use the same .NET Framework assembly unification warning defaults as MSBuild.
$arguments = @('/nologo', '/nowarn:1701,1702', '/target:exe', '/platform:x86', '/langversion:latest', "/out:$output/Verification.exe", "/reference:$output/PuregoldITToolkit.exe")
foreach ($assembly in @('mscorlib', 'System', 'System.Core', 'System.Xaml', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.IO.Compression', 'System.IO.Compression.FileSystem')) {
    $arguments += "/reference:$framework/$assembly.dll"
}
$arguments += "/reference:$framework/Facades/netstandard.dll"
$arguments += "/reference:$output/WinSCPnet.dll"
$arguments += "/reference:$env:USERPROFILE/.nuget/packages/communitytoolkit.mvvm/8.4.2/lib/netstandard2.0/CommunityToolkit.Mvvm.dll"
$arguments += (Join-Path $PSScriptRoot 'Verification.cs')
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Verification compilation failed.' }
& (Join-Path $output 'Verification.exe')
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }

