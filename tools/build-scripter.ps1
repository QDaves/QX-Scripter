#requires -Version 7.3
param(
    [ValidateSet('Debug', 'Release')][string]$configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'prepare-docs.ps1')
$solution = Join-Path $PSScriptRoot '../QX.slnx'
& dotnet restore $solution --locked-mode --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Scripter package restore failed.' }
& dotnet build $solution -c $configuration --no-restore --disable-build-servers -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Scripter build failed.' }
