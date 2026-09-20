#requires -Version 7.3
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')][string]$runtime,
    [Parameter(Mandatory)][ValidateSet('Desktop', 'CLI')][string]$edition,
    [string]$output = (Join-Path $PSScriptRoot '../artifacts'),
    [string]$version
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($output)
$name = "QX-Scripter-$edition-$runtime"
$package = Join-Path $output $name
$mac_bundle = $edition -eq 'Desktop' -and $runtime.StartsWith('osx-')
$windows = $runtime.StartsWith('win-')
$extension = if ($windows) { '.zip' } else { '.tar.gz' }
$archive = Join-Path $output ($name + $extension)
if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $archive)) {
    throw "An output already exists for $name. Choose a fresh output directory."
}
$project = if ($edition -eq 'Desktop') { 'src/QX.Desktop/QX.Desktop.csproj' } else { 'src/QX.App/QX.App.csproj' }
$binary_directory = if ($mac_bundle) { Join-Path $package 'QX Scripter.app/Contents/MacOS' } else { $package }
$arguments = @('publish', (Join-Path $root $project), '-c', 'Release', '-r', $runtime,
    '--self-contained', 'false', '-p:PublishSingleFile=false', '-p:UseAppHost=true',
    '-p:DebugType=None', '-p:DebugSymbols=false', '-p:RestoreLockedMode=true',
    '--disable-build-servers', '-o', $binary_directory)
if ($version) {
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must contain three numeric components.' }
    $arguments += "-p:QxBuildVersion=$version"
}
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed." }

$executable = if ($windows) { 'QX.exe' } else { 'QX' }
foreach ($file in $executable, 'QX.dll', 'QX.deps.json', 'QX.runtimeconfig.json') {
    if (!(Test-Path -LiteralPath (Join-Path $binary_directory $file) -PathType Leaf)) {
        throw "Missing package file: $file"
    }
}
$configuration = Get-Content -LiteralPath (Join-Path $binary_directory 'QX.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($configuration.runtimeOptions.includedFrameworks -or
    !($configuration.runtimeOptions.framework -or $configuration.runtimeOptions.frameworks)) {
    throw 'The package must use the installed .NET runtime.'
}
if ($mac_bundle) {
    $contents = Split-Path $binary_directory
    $resources = Join-Path $contents 'Resources'
    New-Item -ItemType Directory -Path $resources -Force | Out-Null
    $icon = [IO.File]::ReadAllBytes((Join-Path $root 'src/QX.Desktop/Assets/qx.ico'))
    $count = [BitConverter]::ToUInt16($icon, 4)
    $png = $null
    for ($index = 0; $index -lt $count; $index++) {
        $entry = 6 + 16 * $index
        if ($icon[$entry] -ne 0 -or $icon[$entry + 1] -ne 0) { continue }
        $length = [BitConverter]::ToInt32($icon, $entry + 8)
        $offset = [BitConverter]::ToInt32($icon, $entry + 12)
        if ($offset -lt 0 -or $length -lt 8 -or $offset + $length -gt $icon.Length) { throw 'Invalid icon entry.' }
        if ($icon[$offset] -eq 137 -and $icon[$offset + 1] -eq 80) {
            $png = $icon[$offset..($offset + $length - 1)]
            break
        }
    }
    if ($null -eq $png) { throw 'The application icon needs a 256px PNG entry.' }
    $stream = [IO.File]::Create((Join-Path $resources 'qx.icns'))
    try {
        $stream.Write([Text.Encoding]::ASCII.GetBytes('icns'))
        $size = [BitConverter]::GetBytes([int]($png.Length + 16))
        [Array]::Reverse($size)
        $stream.Write($size)
        $stream.Write([Text.Encoding]::ASCII.GetBytes('ic08'))
        $size = [BitConverter]::GetBytes([int]($png.Length + 8))
        [Array]::Reverse($size)
        $stream.Write($size)
        $stream.Write([byte[]]$png)
    } finally { $stream.Dispose() }
    $build_version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $binary_directory 'QX.dll')).ProductVersion.Split('+')[0]
    if ($build_version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid bundle version: $build_version" }
    $plist = Get-Content -LiteralPath (Join-Path $root 'tools/macos/Info.plist') -Raw
    [IO.File]::WriteAllText((Join-Path $contents 'Info.plist'), $plist.Replace('@VERSION@', $build_version))
}

if ($windows) {
    Compress-Archive -LiteralPath $package -DestinationPath $archive -CompressionLevel Optimal
} else {
    Add-Type -AssemblyName System.Formats.Tar
    $stream = [IO.File]::Create($archive)
    $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionLevel]::Optimal)
    $tar = [System.Formats.Tar.TarWriter]::new($gzip, $true)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName) {
            $relative = [IO.Path]::GetRelativePath($output, $file.FullName).Replace('\', '/')
            $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, $relative)
            $entry.Mode = [IO.UnixFileMode]420
            if ($file.Name -eq 'QX' -or $file.Extension -in '.dylib', '.so') {
                $entry.Mode = [IO.UnixFileMode]493
            }
            $entry.ModificationTime = [DateTimeOffset]$file.LastWriteTimeUtc
            $input_stream = $file.OpenRead()
            try {
                $entry.DataStream = $input_stream
                $tar.WriteEntry($entry)
            } finally { $input_stream.Dispose() }
        }
    } finally {
        $tar.Dispose()
        $gzip.Dispose()
        $stream.Dispose()
    }
}
Write-Output $archive
