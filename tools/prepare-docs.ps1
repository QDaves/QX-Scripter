#requires -Version 7.3
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$properties = ([xml][IO.File]::ReadAllText((Join-Path $root 'Framework.props'))).Project.PropertyGroup
$revision = $properties.QxDocumentationRevision
if ($revision -notmatch '^[a-f0-9]{40}$') { throw 'QxDocumentationRevision must be a full Git commit.' }
$source = Join-Path $root "artifacts/framework-docs/$revision"
if (!(Test-Path -LiteralPath (Join-Path $source '.git'))) {
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    & git -C $source init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the documentation checkout.' }
}
& git -C $source sparse-checkout set website/docs
if ($LASTEXITCODE -ne 0) { throw 'Could not configure the documentation checkout.' }
$actual = & git -C $source rev-parse --verify HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or $actual -ne $revision) {
    & git -C $source fetch --quiet --depth 1 --filter=blob:none https://github.com/qxscripter/QX.git $revision
    if ($LASTEXITCODE -ne 0) { throw 'Could not download the pinned documentation.' }
    & git -C $source checkout --quiet --detach FETCH_HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not check out the pinned documentation.' }
}
$actual = & git -C $source rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actual -ne $revision) { throw 'The documentation checkout does not match QxDocumentationRevision.' }
$framework = ([xml][IO.File]::ReadAllText((Join-Path $source 'Framework.props'))).Project.PropertyGroup
if (($framework.QxFrameworkVersion | Where-Object { $_ }) -ne $properties.QxFrameworkVersion) {
    throw 'The documentation version does not match QxFrameworkVersion.'
}
$documents = Join-Path $source 'website/docs'
foreach ($file in 'toc.yml', 'scripts.md', 'extensions.md') {
    if (!(Test-Path -LiteralPath (Join-Path $documents $file) -PathType Leaf)) {
        throw "Missing scripting documentation: $file"
    }
}
Write-Output $documents
