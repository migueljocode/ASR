#!/usr/bin/env bash
set -euo pipefail

: "${HOME:?HOME must be set}"

case "$(uname -s):$(uname -m)" in
  Linux:x86_64) rid=linux-x64 ;;
  Darwin:arm64|Darwin:aarch64) rid=osx-arm64 ;;
  Darwin:x86_64) rid=osx-x64 ;;
  *)
    echo "Unsupported platform: $(uname -s) $(uname -m)" >&2
    exit 1
    ;;
esac

for command in curl unzip install; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "Required command not found: $command" >&2
    exit 1
  fi
done

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

archive="Whisper-${rid}.zip"
release_url="https://github.com/migueljocode/ASR/releases/latest/download"
curl -fsSL "${release_url}/${archive}" -o "${tmp_dir}/${archive}"
unzip -q "${tmp_dir}/${archive}" -d "$tmp_dir"

if [[ ! -f "${tmp_dir}/Whisper" ]]; then
  echo "The release archive does not contain the Whisper executable." >&2
  exit 1
fi

install_dir="${HOME}/.local/bin"
mkdir -p "$install_dir"
install -m 755 "${tmp_dir}/Whisper" "${install_dir}/Whisper"

case ":${PATH}:" in
  *":${install_dir}:"*) ;;
  *)
    profile="${HOME}/.profile"
    case "${SHELL:-}" in
      */bash) profile="${HOME}/.bashrc" ;;
      */zsh) profile="${HOME}/.zshrc" ;;
    esac

    path_line='export PATH="$HOME/.local/bin:$PATH"'
    if ! grep -Fqx "$path_line" "$profile" 2>/dev/null; then
      printf '\n%s\n' "$path_line" >> "$profile"
    fi
    echo "Added ${install_dir} to ${profile}; open a new terminal to use Whisper from PATH."
    ;;
esac

echo "Installed Whisper to ${install_dir}/Whisper"
