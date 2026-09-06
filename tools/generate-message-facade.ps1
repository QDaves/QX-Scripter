#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$repository_root,
    [switch]$check
)

$ErrorActionPreference = 'Stop'
if (!$PSBoundParameters.ContainsKey('repository_root'))
{
    $repository_root = Split-Path -Parent $PSScriptRoot
}
$root = [IO.Path]::GetFullPath($repository_root)
$messages_path = Join-Path $root 'src\QX.Protocol\Resources\messages.ini'
$output_path = Join-Path $root 'src\QX.Protocol\Msg.cs'
$messages_bytes = [IO.File]::ReadAllBytes($messages_path)
if ($messages_bytes.Length -lt 4 -or
    $messages_bytes[0] -ne 0xEF -or
    $messages_bytes[1] -ne 0xBB -or
    $messages_bytes[2] -ne 0xBF)
{
    throw 'Resources/messages.ini must use UTF-8 with BOM.'
}
if ($messages_bytes -contains 0x0D)
{
    throw 'Resources/messages.ini must use LF line endings.'
}
if ($messages_bytes[-1] -ne 0x0A)
{
    throw 'Resources/messages.ini must end with LF.'
}

$directions = [ordered]@{
    Incoming = [Collections.Generic.SortedDictionary[string, Collections.Generic.SortedSet[string]]]::new([StringComparer]::Ordinal)
    Outgoing = [Collections.Generic.SortedDictionary[string, Collections.Generic.SortedSet[string]]]::new([StringComparer]::Ordinal)
}
$compatibility_aliases = [ordered]@{
    Incoming = [ordered]@{
        Heightmap = 'HeightMap'
        Motdnotification = 'MOTDNotification'
    }
    Outgoing = [ordered]@{
        ForwardToAcompetitionRoom = 'ForwardToACompetitionRoom'
        ForwardToArandomPromotedRoom = 'ForwardToARandomPromotedRoom'
        ForwardToAsubmittableRoom = 'ForwardToASubmittableRoom'
        GetUserAchievementsForAresolution = 'GetUserAchievementsForAResolution'
        LoginWithPasswordDeprecated = 'LoginWithPasswordDEPRECATED'
        MoveItemDeprecated = 'MoveItemDEPRECATED'
        PlaceStuffFromStripDeprecated = 'PlaceStuffFromStripDEPRECATED'
    }
}
$direction = $null

foreach ($raw_line in [IO.File]::ReadAllLines($messages_path))
{
    $line = $raw_line.Trim()
    if ($line.Length -eq 0 -or $line[0] -eq ';')
    {
        continue
    }
    if ($line[0] -eq '[')
    {
        if ($line -ne '[Incoming]' -and $line -ne '[Outgoing]')
        {
            throw "Unknown message section '$line'."
        }
        $direction = $line.Substring(1, $line.Length - 2)
        continue
    }
    if ($null -eq $direction)
    {
        throw "Message row '$line' appears before a direction section."
    }

    $comment = $line.IndexOf(';')
    if ($comment -ge 0)
    {
        $line = $line.Substring(0, $comment).Trim()
    }
    if ($line.Length -eq 0)
    {
        continue
    }

    $summary_fields = [Collections.Generic.List[string]]::new()
    $names = [Collections.Generic.List[string]]::new()
    $has_key = $false
    foreach ($field in $line -split '[ \t]+')
    {
        if ($field.StartsWith('!'))
        {
            throw "Separate message alias '$field' is not supported."
        }

        $colon = $field.IndexOf(':')
        if ($colon -le 0)
        {
            throw "Message field '$field' is malformed."
        }

        $runes = $field.Substring(0, $colon)
        $name = $field.Substring($colon + 1)
        if ($runes -eq 'k')
        {
            if ($has_key)
            {
                throw "Message row '$line' declares more than one stable key."
            }
            if ([string]::IsNullOrWhiteSpace($name) -or
                $name.StartsWith('.') -or
                $name.EndsWith('.') -or
                $name.Contains('..') -or
                $name -notmatch '^[A-Za-z0-9._-]+$')
            {
                throw "Message row '$line' declares an invalid stable key."
            }
            $has_key = $true
            continue
        }
        if ($runes -ne 'u' -and $runes -ne 'f' -and $runes -ne 'uf')
        {
            throw "Message field '$field' uses unsupported client runes '$runes'."
        }
        if ($name.Length -eq 0)
        {
            throw "Message field '$field' has no alias name."
        }
        if ($name -eq '-')
        {
            continue
        }

        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$')
        {
            throw "Message name '$name' is not a valid C# identifier."
        }

        $summary_fields.Add("$runes`:$name")
        if (!$names.Contains($name))
        {
            $names.Add($name)
        }
    }

    if ($summary_fields.Count -eq 0)
    {
        throw "Message row '$line' has no Flash or Unity aliases."
    }
    $summary = $summary_fields -join ' '
    foreach ($name in $names)
    {
        $summaries = $null
        if (!$directions[$direction].TryGetValue($name, [ref]$summaries))
        {
            $summaries = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
            $directions[$direction][$name] = $summaries
        }
        $summaries.Add($summary) | Out-Null
    }
}

foreach ($section in $compatibility_aliases.Keys)
{
    foreach ($alias in $compatibility_aliases[$section].Keys)
    {
        $canonical = $compatibility_aliases[$section][$alias]
        $summaries = $null
        if (!$directions[$section].TryGetValue($canonical, [ref]$summaries))
        {
            throw "Compatibility alias '$alias' targets missing message '$canonical'."
        }
        if ($directions[$section].ContainsKey($alias))
        {
            throw "Compatibility alias '$alias' already exists in the manifest."
        }
        $directions[$section].Add($alias, $summaries)
    }
}

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('namespace Qx.Protocol;')
$lines.Add('')
$lines.Add('/// <summary>')
$lines.Add('/// Compile-checked message name constants generated from <c>Resources/messages.ini</c>.')
$lines.Add('/// Each constant carries the exact spelling used by Flash or Unity.')
$lines.Add('/// </summary>')
$lines.Add('public static class Msg')
$lines.Add('{')
foreach ($entry in @(@('Incoming', 'In', 'server to client'), @('Outgoing', 'Out', 'client to server')))
{
    $section = $entry[0]
    $class_name = $entry[1]
    $description = $entry[2]
    $lines.Add("    /// <summary>$section message names ($description).</summary>")
    $lines.Add("    public static class $class_name")
    $lines.Add('    {')
    foreach ($name in $directions[$section].Keys)
    {
        $summary = [Security.SecurityElement]::Escape(($directions[$section][$name] -join ' | '))
        $lines.Add("        /// <summary>$summary</summary>")
        $lines.Add("        public const string $name = `"$name`";")
    }
    $lines.Add('    }')
    if ($section -eq 'Incoming')
    {
        $lines.Add('')
    }
}
$lines.Add('}')
$lines.Add('')

$encoding = [Text.UTF8Encoding]::new($true)
[byte[]]$content = $encoding.GetPreamble() + $encoding.GetBytes(($lines -join "`r`n"))
if ($check)
{
    if (![IO.File]::Exists($output_path))
    {
        throw "Generated message facade '$output_path' does not exist."
    }
    $current = [IO.File]::ReadAllBytes($output_path)
    if ($current.Length -ne $content.Length -or
        [Convert]::ToBase64String($current) -cne [Convert]::ToBase64String($content))
    {
        throw "Generated message facade '$output_path' is stale."
    }
    return
}

[IO.File]::WriteAllBytes($output_path, $content)
