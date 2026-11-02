<#
Checks local source/configuration fingerprints without printing file contents.
Only -Refresh writes docs/PROJECT_CONTEXT.sources.json. Payloads are excluded;
this is a context freshness check, not a payload integrity or security scan.
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$Refresh,
    [ValidateRange(0, 2147483647)][int]$MaxChanges = 25
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rootDirectory = Get-Item -LiteralPath $Root -Force
if (-not $rootDirectory.PSIsContainer -or $rootDirectory.PSProvider.Name -ne 'FileSystem') {
    throw 'Root must be a local filesystem directory.'
}
if ($rootDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Root must not be a reparse directory.'
}
$rootPrefix = $rootDirectory.FullName
if (-not $rootPrefix.EndsWith([IO.Path]::DirectorySeparatorChar.ToString())) {
    $rootPrefix += [IO.Path]::DirectorySeparatorChar
}
$docsPath = Join-Path $rootDirectory.FullName 'docs'
$baselinePath = Join-Path $docsPath 'PROJECT_CONTEXT.sources.json'
if (Test-Path -LiteralPath $docsPath) {
    $docsDirectory = Get-Item -LiteralPath $docsPath -Force
    if (-not $docsDirectory.PSIsContainer -or ($docsDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The baseline docs path must be a regular directory.'
    }
}

$extensions = @(
    '.cs', '.csproj', '.props', '.targets', '.xaml', '.manifest', '.config',
    '.ps1', '.psm1', '.psd1', '.js', '.jsx', '.mjs', '.cjs', '.ts', '.tsx',
    '.css', '.scss', '.sass', '.less', '.html', '.htm', '.json', '.jsonc',
    '.yaml', '.yml', '.md', '.mdx', '.bat', '.cmd', '.py', '.sln', '.slnx',
    '.toml', '.xml', '.resx', '.reg', '.rc', '.h', '.hpp', '.c', '.cpp', '.txt', '.ini'
)
$safeDotFiles = @(
    '.editorconfig', '.gitignore', '.gitattributes', '.dockerignore', '.ignore',
    '.rgignore', '.prettierignore', '.eslintignore', '.stylelintignore',
    '.browserslistrc', '.nvmrc', '.node-version', '.python-version', '.tool-versions'
)
$safeDotPattern = '^\.(prettierrc|eslintrc|stylelintrc|babelrc|lintstagedrc|markdownlint)(\.(json|jsonc|ya?ml|js|mjs|cjs|ts|toml))?$'
$excludedNames = @(
    '.build', '.private', '.git', 'node_modules', 'bin', 'obj', 'dist',
    'artifacts', 'test-results', 'playwright-report', 'test-output',
    'integration-preview', '工具', '重装'
)
$excludedPaths = @('assets/web', 'assets/icons', 'frontend/public')
$current = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
$pending = [Collections.Generic.Stack[string]]::new()
$pending.Push($rootDirectory.FullName)
while ($pending.Count -gt 0) {
    foreach ($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
        # Prune before entering directories; never follow links or reparse points.
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
        $relative = $item.FullName.Substring($rootPrefix.Length).Replace('\', '/')
        if ($item.PSIsContainer) {
            if ($item.Name -in $excludedNames -or $item.Name -like '.delivery*' -or $relative -in $excludedPaths) { continue }
            $pending.Push($item.FullName)
        } elseif ($item.FullName -ne $baselinePath) {
            # Dot files use an allowlist so .env, .npmrc and credential files are not fingerprinted by default.
            $isContextFile = $item.Extension -in $extensions
            if ($item.Name.StartsWith('.')) { $isContextFile = $item.Name -in $safeDotFiles -or $item.Name -match $safeDotPattern }
            if ($isContextFile -or $relative -eq 'assets/update-public.pem') {
                $current.Add($relative, (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash)
            }
        }
    }
}

$changes = [ordered]@{
    Added = [Collections.Generic.List[string]]::new()
    Modified = [Collections.Generic.List[string]]::new()
    Deleted = [Collections.Generic.List[string]]::new()
}
$state = 'missing-baseline'
if (-not $Refresh -and (Test-Path -LiteralPath $baselinePath)) {
    $baselineFile = Get-Item -LiteralPath $baselinePath -Force
    if ($baselineFile.PSIsContainer -or ($baselineFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The baseline must be a regular file.'
    }
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ($null -eq $baseline -or $baseline.SchemaVersion -ne 1 -or $baseline.Algorithm -ne 'SHA256' -or $null -eq $baseline.Files -or $baseline.Files -isnot [pscustomobject]) {
        throw 'Unsupported context baseline. Run -Refresh after reviewing context changes.'
    }
    $previous = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $baseline.Files.PSObject.Properties) {
        if ([string]::IsNullOrWhiteSpace($entry.Name) -or $entry.Value -isnot [string] -or $entry.Value -notmatch '^[A-Fa-f0-9]{64}$') {
            throw 'Invalid file fingerprint in the context baseline.'
        }
        $previous.Add($entry.Name, $entry.Value)
    }
    foreach ($path in ($current.Keys | Sort-Object)) {
        if (-not $previous.ContainsKey($path)) { $changes.Added.Add($path) }
        elseif ($current[$path] -ne $previous[$path]) { $changes.Modified.Add($path) }
    }
    foreach ($path in ($previous.Keys | Sort-Object)) {
        if (-not $current.ContainsKey($path)) { $changes.Deleted.Add($path) }
    }
    $state = 'unchanged'
    if ($changes.Added.Count + $changes.Modified.Count + $changes.Deleted.Count -gt 0) { $state = 'changed' }
}

if ($Refresh) {
    if (Test-Path -LiteralPath $baselinePath) {
        $baselineFile = Get-Item -LiteralPath $baselinePath -Force
        if ($baselineFile.PSIsContainer -or ($baselineFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'The baseline must be a regular file.'
        }
    }
    $snapshot = [ordered]@{}
    foreach ($path in ($current.Keys | Sort-Object)) { $snapshot[$path] = $current[$path] }
    $baselineJson = [ordered]@{ SchemaVersion = 1; Algorithm = 'SHA256'; Files = $snapshot } | ConvertTo-Json -Depth 4
    $null = New-Item -ItemType Directory -Path $docsPath -Force
    $temporaryPath = $baselinePath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($temporaryPath, $baselineJson, [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporaryPath -Destination $baselinePath -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
    $state = 'unchanged'
}

$changeCount = $changes.Added.Count + $changes.Modified.Count + $changes.Deleted.Count
$remaining = $MaxChanges
$visibleChanges = [ordered]@{}
foreach ($kind in @('Added', 'Modified', 'Deleted')) {
    $visibleChanges[$kind] = @($changes[$kind] | Select-Object -First $remaining)
    $remaining -= $visibleChanges[$kind].Count
}
$result = [ordered]@{
    State = $state
    FileCount = $current.Count
    ChangeCount = $changeCount
    Changes = $visibleChanges
    Truncated = ($changeCount -gt $MaxChanges)
}
if ($Refresh) { $result.Refreshed = $true }
$result | ConvertTo-Json -Depth 4 -Compress
