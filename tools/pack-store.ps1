#requires -Version 7.3
param(
    [string]$output = (Join-Path $PSScriptRoot '../artifacts/store'),
    [string]$version
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($output)
$project = Join-Path $root 'src/QX.Desktop/QX.Desktop.csproj'
$stage = Join-Path $output 'extension'
$hosts = Join-Path $output 'hosts'
$archive = Join-Path $output 'extension.zip'
$runtimes = 'win-x64', 'win', 'linux-x64', 'osx'
$starters = [ordered]@{ 'win-x64' = 'QX.exe'; 'linux-x64' = 'QX-linux-x64'; 'osx-x64' = 'QX-osx-x64'; 'osx-arm64' = 'QX-osx-arm64' }
if (Test-Path -LiteralPath $output) {
    throw "An output already exists at $output. Choose a fresh output directory."
}
if ($version -and $version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must contain three numeric components.' }

function Publish-Desktop([string]$directory, [string]$runtime) {
    $arguments = @('publish', $project, '-c', 'Release', '--self-contained', 'false',
        '-p:PublishSingleFile=false', '-p:UseAppHost=true', '-p:DebugType=None', '-p:DebugSymbols=false',
        '-p:RestoreLockedMode=true', '--disable-build-servers', '-o', $directory)
    if ($runtime) { $arguments += '-r', $runtime }
    if ($version) { $arguments += "-p:QxBuildVersion=$version" }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Publishing $(if ($runtime) { $runtime } else { 'the portable layout' }) failed." }
}

Publish-Desktop $stage $null
foreach ($runtime in $starters.Keys) {
    Publish-Desktop (Join-Path $hosts $runtime) $runtime
}

Get-ChildItem -LiteralPath (Join-Path $stage 'runtimes') -Directory |
    Where-Object { $_.Name -notin $runtimes } |
    Remove-Item -Recurse -Force
foreach ($runtime in $runtimes) {
    if (!(Test-Path -LiteralPath (Join-Path $stage "runtimes/$runtime") -PathType Container)) {
        throw "Missing runtime assets: runtimes/$runtime"
    }
}
foreach ($name in 'QX.exe', 'QX') {
    Remove-Item -LiteralPath (Join-Path $stage $name) -Force -ErrorAction Ignore
}
foreach ($runtime in $starters.Keys) {
    $host_name = if ($runtime -eq 'win-x64') { 'QX.exe' } else { 'QX' }
    Copy-Item -LiteralPath (Join-Path $hosts "$runtime/$host_name") -Destination (Join-Path $stage $starters[$runtime])
}
Remove-Item -LiteralPath $hosts -Recurse -Force

foreach ($file in @('QX.dll', 'QX.deps.json', 'QX.runtimeconfig.json') + @($starters.Values)) {
    if (!(Test-Path -LiteralPath (Join-Path $stage $file) -PathType Leaf)) {
        throw "Missing package file: $file"
    }
}
$configuration = Get-Content -LiteralPath (Join-Path $stage 'QX.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($configuration.runtimeOptions.includedFrameworks -or
    !($configuration.runtimeOptions.framework -or $configuration.runtimeOptions.frameworks)) {
    throw 'The package must use the installed .NET runtime.'
}
$dependencies = Get-Content -LiteralPath (Join-Path $stage 'QX.deps.json') -Raw | ConvertFrom-Json
if ($dependencies.runtimeTarget.name -match '/') {
    throw "The package must stay portable, but the dependency manifest targets $($dependencies.runtimeTarget.name)."
}
$build_version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'QX.dll')).ProductVersion.Split('+')[0]
if ($build_version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid package version: $build_version" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -LiteralPath $stage -Recurse -Force

$manifest = Get-Content -LiteralPath (Join-Path $root 'tools/store/extension.json') -Raw
$updated = [DateTime]::UtcNow.ToString('dd-MM-yyyy HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
[IO.File]::WriteAllText((Join-Path $output 'extension.json'), $manifest.Replace('@VERSION@', $build_version).Replace('@UPDATED@', $updated))
Copy-Item -LiteralPath (Join-Path $root 'tools/store/icon.png') -Destination (Join-Path $output 'icon.png')
Write-Output $archive
