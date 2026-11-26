$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') { throw 'This installer is for Windows.' }
if ($env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'This release supports Windows x64 only.' }

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\ASR'
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())

try {
    New-Item -ItemType Directory -Force -Path $installDir, $tempDir | Out-Null
    $archive = Join-Path $tempDir 'Whisper-win-x64.zip'
    $extractDir = Join-Path $tempDir 'extracted'
    $downloadUrl = 'https://github.com/migueljocode/ASR/releases/latest/download/Whisper-win-x64.zip'
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $archive
    Expand-Archive -Path $archive -DestinationPath $extractDir

    $sourceExe = Join-Path $extractDir 'Whisper.exe'
    if (-not (Test-Path -LiteralPath $sourceExe)) {
        throw 'The release archive does not contain Whisper.exe.'
    }
    Copy-Item -LiteralPath $sourceExe -Destination (Join-Path $installDir 'Whisper.exe') -Force

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $normalizedEntries = @($userPath -split ';' | ForEach-Object { $_.TrimEnd('\') })
    if ($normalizedEntries -notcontains $installDir.TrimEnd('\')) {
        $newUserPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $installDir } else { "$userPath;$installDir" }
        [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User')
    }
    if (($env:Path -split ';' | ForEach-Object { $_.TrimEnd('\') }) -notcontains $installDir.TrimEnd('\')) {
        $env:Path = "$installDir;$env:Path"
    }

    Write-Host "Installed Whisper to $installDir. Try: Whisper --help"
} finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
