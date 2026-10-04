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
$keys_path = Join-Path $root 'src\QX.Protocol\MessageKeys.cs'
$contracts_path = Join-Path $root 'src\QX.Game\Protocol\MessageContracts.cs'
$keys_without_contract = @(
    'friends.room_invite.received'
    'notifications.dialog'
    'room.advertisement'
    'room.occupants.pet.respect'
)

function read_fields([string]$source, [string]$field_type, [string]$file_name)
{
    $fields = [Collections.Specialized.OrderedDictionary]::new([StringComparer]::Ordinal)
    $scopes = [Collections.Generic.List[string]]::new()
    $pending_class = $null
    $pending_field = $null
    $pattern = '"(?:[^"\\\r\n]|\\.)*"|//[^\r\n]*|\bclass\s+(?<class>\w+)|' +
        '\bpublic\s+static\s+readonly\s+' + $field_type + '\s+(?<field>\w+)\s*=|(?<open>\{)|(?<close>\})|(?<end>;)'
    foreach ($match in [regex]::Matches($source, $pattern))
    {
        if ($match.Groups['class'].Success)
        {
            $pending_class = $match.Groups['class'].Value
        }
        elseif ($match.Groups['open'].Success)
        {
            $scopes.Add($pending_class)
            $pending_class = $null
        }
        elseif ($match.Groups['close'].Success)
        {
            if ($scopes.Count -eq 0 -or ($pending_field -and $scopes.Count -eq $pending_field.depth))
            {
                $line = $source.Substring(0, $match.Index).Split("`n").Length
                throw "$file_name could not be parsed: the '}' on line $line has no matching '{'."
            }
            $scopes.RemoveAt($scopes.Count - 1)
        }
        elseif ($match.Groups['field'].Success)
        {
            $classes = @($scopes | Where-Object { $_ } | Select-Object -Skip 1)
            $pending_field = @{
                path = ($classes + $match.Groups['field'].Value) -join '.'
                start = $match.Index + $match.Length
                depth = $scopes.Count
            }
        }
        elseif ($match.Groups['end'].Success -and $pending_field -and $scopes.Count -eq $pending_field.depth)
        {
            $fields[$pending_field.path] = $source.Substring($pending_field.start, $match.Index - $pending_field.start)
            $pending_field = $null
        }
    }
    if ($scopes.Count -ne 0)
    {
        throw "$file_name could not be parsed: a '{' is never closed."
    }
    return $fields
}

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
$ini_keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
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
            $ini_keys.Add($name) | Out-Null
            $has_key = $true
            continue
        }
        if ($runes -ne 'f')
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

        if (!$names.Contains($name))
        {
            $names.Add($name)
        }
    }

    if ($names.Count -eq 0)
    {
        throw "Message row '$line' has no Flash aliases."
    }
    $primary = $names[0]
    $other_names = $null
    if (!$directions[$direction].TryGetValue($primary, [ref]$other_names))
    {
        $other_names = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
        $directions[$direction][$primary] = $other_names
    }
    foreach ($other in $names)
    {
        if ($other -cne $primary)
        {
            $other_names.Add($other) | Out-Null
        }
    }
}

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('namespace Qx.Protocol;')
$lines.Add('')
$lines.Add('/// <summary>Provides the Flash message names as compile-checked constants.</summary>')
$lines.Add('/// <remarks>Generated from <c>Resources/messages.ini</c>. Every constant is spelled exactly as in the Flash client. A message the client knows by several names has one constant, named after its primary name, and its summary lists the other names.</remarks>')
$lines.Add('public static class Msg')
$lines.Add('{')
foreach ($entry in @(@('Incoming', 'In', 'incoming', 'server to the client'), @('Outgoing', 'Out', 'outgoing', 'client to the server')))
{
    $section = $entry[0]
    $class_name = $entry[1]
    $adjective = $entry[2]
    $route = $entry[3]
    $lines.Add("    /// <summary>Provides the $adjective message names, sent from the $route.</summary>")
    $lines.Add("    public static class $class_name")
    $lines.Add('    {')
    foreach ($name in $directions[$section].Keys)
    {
        $other_names = @($directions[$section][$name] | ForEach-Object { "<c>$_</c>" })
        $also = if ($other_names.Count -eq 0) { '' } else { ', also named ' + ($other_names -join ' and ') }
        $lines.Add("        /// <summary>The $adjective Flash message <c>$name</c>$also.</summary>")
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
    $problems = [Collections.Generic.List[string]]::new()
    if (![IO.File]::Exists($output_path))
    {
        $problems.Add("Generated message facade '$output_path' does not exist.")
    }
    else
    {
        $current = [IO.File]::ReadAllBytes($output_path)
        if ($current.Length -ne $content.Length -or
            [Convert]::ToBase64String($current) -cne [Convert]::ToBase64String($content))
        {
            $problems.Add("Generated message facade '$output_path' is stale.")
        }
    }

    $key_paths = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $keys = read_fields ([IO.File]::ReadAllText($keys_path)) 'MessageKey' 'MessageKeys.cs'
    foreach ($path in $keys.Keys)
    {
        if ($keys[$path] -notmatch '^\s*new\(\s*"(?<key>[^"]+)"\s*\)\s*$')
        {
            $problems.Add("MessageKeys.$path is not declared with a literal key.")
            continue
        }
        $key = $Matches['key']
        if ($key_paths.ContainsKey($key))
        {
            $problems.Add("MessageKeys.$path repeats the key '$key' of MessageKeys.$($key_paths[$key]).")
            continue
        }
        $key_paths.Add($key, $path)
        if (!$ini_keys.Contains($key))
        {
            $problems.Add("MessageKeys.$path declares '$key', which messages.ini does not declare.")
        }
    }
    foreach ($key in $ini_keys)
    {
        if (!$key_paths.ContainsKey($key))
        {
            $problems.Add("messages.ini declares '$key', which MessageKeys does not declare.")
        }
    }

    $contracts_source = [IO.File]::ReadAllText($contracts_path)
    $contracts = read_fields $contracts_source 'MessageContract<[^>]+>' 'MessageContracts.cs'
    foreach ($path in $contracts.Keys)
    {
        $binding = $contracts[$path]
        if ($binding -match '^\s*(?<factory>\w+)\(\)\s*$')
        {
            $binding = [regex]::Match($contracts_source, "\b$($Matches['factory'])\(\)\s*=>(?<body>[^;]*);").Groups['body'].Value
        }
        $bound = @([regex]::Matches($binding, '\bMessageKeys\.(?<path>[\w.]+)') | ForEach-Object { $_.Groups['path'].Value })
        if ($bound.Count -ne 1)
        {
            $problems.Add("MessageContracts.$path must bind exactly one MessageKeys member.")
        }
        elseif ($bound[0] -cne $path)
        {
            $problems.Add("MessageContracts.$path binds MessageKeys.$($bound[0]), so it belongs at MessageContracts.$($bound[0]).")
        }
    }

    $all = [regex]::Match($contracts_source, '\bAll\s*\{\s*get;\s*\}\s*=\s*\[(?<paths>[^\]]*)\]')
    if (!$all.Success)
    {
        throw 'MessageContracts.All must be declared as a collection expression.'
    }
    $listed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in [regex]::Matches($all.Groups['paths'].Value, '[\w.]+').Value)
    {
        if (!$listed.Add($path))
        {
            $problems.Add("MessageContracts.All lists $path more than once.")
        }
        elseif (!$contracts.Contains($path))
        {
            $problems.Add("MessageContracts.All lists $path, which is not a declared contract.")
        }
    }
    foreach ($path in $contracts.Keys)
    {
        if (!$listed.Contains($path))
        {
            $problems.Add("MessageContracts.$path is missing from MessageContracts.All.")
        }
    }

    foreach ($key in $key_paths.Keys)
    {
        $path = $key_paths[$key]
        $has_contract = $contracts.Contains($path)
        $without_contract = $keys_without_contract -ccontains $key
        if (!$has_contract -and !$without_contract)
        {
            $problems.Add("MessageKeys.$path has no contract at MessageContracts.$path.")
        }
        elseif ($has_contract -and $without_contract)
        {
            $problems.Add("MessageKeys.$path is listed as a key without a contract, but MessageContracts.$path binds it.")
        }
    }
    foreach ($key in $keys_without_contract)
    {
        if (!$key_paths.ContainsKey($key))
        {
            $problems.Add("The key '$key' is listed as a key without a contract, but MessageKeys does not declare it.")
        }
    }

    if ($problems.Count -ne 0)
    {
        foreach ($problem in $problems)
        {
            Write-Error $problem -ErrorAction Continue
        }
        throw 'The message facade check failed.'
    }
    return
}

[IO.File]::WriteAllBytes($output_path, $content)
