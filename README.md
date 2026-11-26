# ASR

ASR is a command-line speech-to-text application powered by [Whisper](https://github.com/openai/whisper). It runs recognition locally using Whisper.net and downloaded Whisper models.

It can transcribe audio files to the terminal or to a text/subtitle file, and it supports live microphone transcription on Linux.

## Quick install

These commands install the `v0.0.0` release for the current user. Windows uses PowerShell; Linux and macOS use Bash or Zsh. FFmpeg is still required for audio files, and Linux microphone capture also requires `arecord`.

**Windows (PowerShell):**

```powershell
$ErrorActionPreference = 'Stop'
if ($env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'This release supports Windows x64 only.' }
$version = 'v0.0.0'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\ASR'
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
try {
    New-Item -ItemType Directory -Force -Path $installDir, $tempDir | Out-Null
    $archive = Join-Path $tempDir 'Whisper.zip'
    $extractDir = Join-Path $tempDir 'extracted'
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/migueljocode/ASR/releases/download/$version/Whisper-win-x64.zip" -OutFile $archive
    Expand-Archive -Path $archive -DestinationPath $extractDir
    Copy-Item -Path (Join-Path $extractDir 'Whisper.exe') -Destination (Join-Path $installDir 'Whisper.exe') -Force

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (($userPath -split ';') -notcontains $installDir) {
        $newUserPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $installDir } else { "$userPath;$installDir" }
        [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User')
    }
    if (($env:Path -split ';') -notcontains $installDir) { $env:Path = "$installDir;$env:Path" }
    Write-Host "Installed. Try: Whisper --help"
} finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
```

**Linux and macOS (Bash or Zsh):**

```sh
set -eu
version=v0.0.0
case "$(uname -s):$(uname -m)" in
  Linux:x86_64) rid=linux-x64 ;;
  Darwin:arm64|Darwin:aarch64) rid=osx-arm64 ;;
  Darwin:x86_64) rid=osx-x64 ;;
  *) echo "Unsupported platform: $(uname -s) $(uname -m)" >&2; exit 1 ;;
esac
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT
curl -fsSL "https://github.com/migueljocode/ASR/releases/download/${version}/Whisper-${rid}.zip" -o "$tmp_dir/Whisper.zip"
unzip -q "$tmp_dir/Whisper.zip" -d "$tmp_dir"
mkdir -p "$HOME/.local/bin"
install -m 755 "$tmp_dir/Whisper" "$HOME/.local/bin/Whisper"

case ":$PATH:" in
  *":$HOME/.local/bin:"*) ;;
  *)
    profile="$HOME/.profile"
    case "${SHELL:-}" in
      */bash) profile="$HOME/.bashrc" ;;
      */zsh) profile="$HOME/.zshrc" ;;
    esac
    if ! grep -Fqx 'export PATH="$HOME/.local/bin:$PATH"' "$profile" 2>/dev/null; then
      printf '\nexport PATH="$HOME/.local/bin:$PATH"\n' >> "$profile"
    fi
    echo "Added ~/.local/bin to $profile; open a new terminal to use Whisper from PATH."
    ;;
esac
echo "Installed to $HOME/.local/bin/Whisper"
```

Run `Whisper --help` in a new terminal after installation.

## Features

- Local speech recognition after a model has been downloaded.
- Audio file input decoded by FFmpeg.
- Plain-text (`.txt`) and SubRip subtitle (`.srt`) output.
- Live microphone transcription on Linux using the default ALSA/PipeWire device.
- Six downloadable models, from `tiny` to `large`, with resumable downloads.
- Single-file, self-contained release builds for Linux x64, Windows x64, macOS x64, and macOS ARM64.

## Manual install

Download and extract the archive for your platform from the [GitHub Releases](https://github.com/migueljocode/ASR/releases) page. The program executable is named `Whisper` (`Whisper.exe` on Windows).

FFmpeg must be installed and available on `PATH` to decode audio files. Live microphone capture additionally requires `arecord` and is currently intended for Linux systems with ALSA/PipeWire. The microphone mode is not currently supported on Windows or macOS.

Alternatively, build from source with the .NET 10 SDK:

```sh
git clone https://github.com/migueljocode/ASR.git
cd ASR
dotnet build Whisper.slnx --configuration Release
```

The build output is under `src/Whisper/bin/Release/`. FFmpeg and, for Linux microphone capture, `arecord` are external runtime dependencies.

## Quick start

Download a model first:

```sh
Whisper --download small
```

Transcribe an audio file and print the result:

```sh
Whisper recording.mp3
```

Save plain text or subtitles (the output extension selects the format):

```sh
Whisper recording.m4a transcript.txt
Whisper recording.wav subtitles.srt
```

Choose a model and language:

```sh
Whisper recording.mp3 transcript.srt --model medium --language fa
```

Transcribe from the default Linux microphone and save text:

```sh
Whisper --mic notes.txt --language en
```

Press Enter to stop microphone capture. Live microphone output is also printed as it is recognized.

## Command-line reference

```text
Whisper <input> [output]
Whisper --input <input> [output]
Whisper --mic [output]
Whisper --download [model]
Whisper --uninstall [model]
Whisper --help
```

| Option | Description |
| --- | --- |
| `<input>` | Audio file to transcribe. FFmpeg handles supported input formats. |
| `-i`, `--input <path>` | Specify the input file by option instead of positionally. |
| `[output]` | Optional output path. `.srt` produces SubRip subtitles; other extensions produce plain text. Without a path, text is printed to the terminal only. |
| `--mic` | Transcribe live from the default Linux microphone. Optional output must be a text file. |
| `-p`, `--model <name>` | Prefer an installed model: `tiny`, `base`, `small`, `medium`, `turbo`, or `large`. |
| `-l`, `--language <code>` | Recognition language code. Defaults to `auto`; examples include `en`, `fa`, and `tr`. |
| `--download [model]` | Download a named model, or interactively select one if omitted. |
| `--uninstall [model]` | Remove a named model, or interactively select one if omitted. |
| `-h`, `--help` | Display command help. |

If a requested model is not installed, the application reports that and uses an available model. When several models are installed, it may prompt for a selection. File transcription defaults to `large` when available; microphone transcription defaults to `turbo`. If neither recommended model is installed, an available model is selected.

Models are stored per user and shared by all installations. Downloads come from the [whisper.cpp Hugging Face model repository](https://huggingface.co/ggerganov/whisper.cpp). Larger models generally provide better accuracy but require more disk space and processing time.

## Build and publish

Build the solution:

```sh
dotnet build Whisper.slnx --configuration Release
```

Publish a self-contained, single-file build for a runtime identifier:

```sh
dotnet publish src/Whisper/Whisper.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output publish/linux-x64
```

The GitHub Actions workflow builds archives for `linux-x64`, `win-x64`, `osx-x64`, and `osx-arm64`. Push a version tag such as `v1.0.0` to create a GitHub Release with all four archives attached:

```sh
git tag v1.0.0
git push origin v1.0.0
```

## Test audio

`tests/asr_samples/` contains the English, Persian, silence, and combined sample audio. `tests/songgenerator.py` can regenerate the speech samples using the `edge-tts` Python package and FFmpeg.
