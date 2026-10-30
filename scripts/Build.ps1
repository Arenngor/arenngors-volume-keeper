param([switch]$Release)
$ErrorActionPreference='Stop'
$repo = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $repo 'VERSION') -Raw).Trim()
if ($version -notmatch '^(\d+)\.(\d+)\.(\d+)(-[A-Za-z0-9.-]+)?$') { throw 'Invalid VERSION.' }
$fileVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
$build = Join-Path $repo 'build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$metadata = @"
using System.Reflection;
[assembly: AssemblyTitle("Arenngor's Volume Keeper")]
[assembly: AssemblyProduct("Arenngor's Volume Keeper")]
[assembly: AssemblyCopyright("Copyright 2026 Arenngor")]
[assembly: AssemblyVersion("$fileVersion")]
[assembly: AssemblyFileVersion("$fileVersion")]
[assembly: AssemblyInformationalVersion("$version")]
namespace DiscordKeeper { internal static class BuildInfo { public const string Version = "$version"; } }
"@
$generated = Join-Path $build 'Version.g.cs'
Set-Content -LiteralPath $generated -Value $metadata -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Install the Windows .NET Framework compiler to build this app.' }
$output = Join-Path $build 'DiscordVolumeKeeper.exe'
$files = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Filter '*.cs' | ForEach-Object FullName) + $generated
$argsList = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/warnaserror+',"/out:$output",('/win32icon:'+(Join-Path $repo 'src\App.ico')),('/win32manifest:'+(Join-Path $repo 'src\app.manifest')),
 '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Management.dll','/reference:System.Web.Extensions.dll') + $files
& $compiler @argsList
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built $output ($version)"
if ($Release) { & (Join-Path $PSScriptRoot 'Package.ps1') }
