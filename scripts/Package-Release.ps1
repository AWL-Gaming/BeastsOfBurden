param(
    [string]$BinaryPath,
    [string]$OutputDirectory,
    [string]$ExpectedBinarySha256
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number
if (!$BinaryPath) { $BinaryPath = Join-Path $root 'BeastsOfBurden/bin/Release/net472/BeastsOfBurden.dll' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root "artifacts/$version" }
$BinaryPath = (Resolve-Path $BinaryPath).Path
$binaryVersion = [Reflection.AssemblyName]::GetAssemblyName($BinaryPath).Version
if ("$($binaryVersion.Major).$($binaryVersion.Minor).$($binaryVersion.Build)" -ne $version) {
    throw 'DLL and manifest versions must match.'
}
$hash = (Get-FileHash $BinaryPath -Algorithm SHA256).Hash
if ($ExpectedBinarySha256 -and $hash -ne $ExpectedBinarySha256) { throw 'DLL hash does not match the tested binary.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($kind in @('manual','thunderstore','hexium')) {
    $name = if ($kind -eq 'manual') { "BeastsOfBurden-$version.zip" } else { "BeastsOfBurden-$version-$kind.zip" }
    $path = Join-Path $OutputDirectory $name
    if (Test-Path $path) { throw "Refusing to replace an existing package: $path" }
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = [ordered]@{}
        foreach ($file in @('README.md','CHANGELOG.md','NOTICE.md','icon.png')) { $files[$file] = Join-Path $root $file }
        if ($kind -eq 'manual') { $files['BepInEx/plugins/BeastsOfBurden/BeastsOfBurden.dll'] = $BinaryPath }
        else { $files['BeastsOfBurden.dll'] = $BinaryPath }
        foreach ($entryName in $files.Keys) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $files[$entryName], $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        if ($kind -ne 'manual') {
            $packageManifest = Get-Content (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
            if ($kind -eq 'hexium') { $packageManifest.dependencies = @() }
            $entry = $archive.CreateEntry('manifest.json')
            $writer = New-Object IO.StreamWriter($entry.Open(), $utf8)
            try { $writer.Write(($packageManifest | ConvertTo-Json -Depth 5)) } finally { $writer.Dispose() }
        }
        if ($kind -eq 'hexium') {
            $faqRoot = Join-Path $root 'Hexium'
            foreach ($file in Get-ChildItem (Join-Path $faqRoot 'faq') -Recurse -File) {
                $relative = $file.FullName.Substring($faqRoot.Length + 1).Replace('\','/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
    } finally { $archive.Dispose() }
    $archive = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\') -or $entry.FullName.StartsWith('/') -or $entry.FullName -match '(^|/)\.\.(/|$)') { throw "Invalid ZIP path: $($entry.FullName)" }
        }
        $dll = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.dll') })
        if ($dll.Count -ne 1) { throw 'Each package must contain exactly one DLL.' }
        $stream = $dll[0].Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $packedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $stream.Dispose(); $sha.Dispose() }
        if ($packedHash -ne $hash) { throw 'Packaged DLL hash mismatch.' }
        [PSCustomObject]@{ Package=$name; Sha256=(Get-FileHash $path).Hash; DllSha256=$packedHash; Entries=@($archive.Entries.FullName) }
    } finally { $archive.Dispose() }
}