#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET_DIR="$SCRIPT_DIR/../Resources/Raw/Models"
mkdir -p "$TARGET_DIR"

python3 -m pip install --upgrade huggingface_hub
huggingface-cli download \
  Qwen/Qwen2.5-1.5B-Instruct-GGUF \
  qwen2.5-1.5b-instruct-q4_k_m.gguf \
  --local-dir "$TARGET_DIR" \
  --local-dir-use-symlinks False

echo "Modell gespeichert unter: $TARGET_DIR"
