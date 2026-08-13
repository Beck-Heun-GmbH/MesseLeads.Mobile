$ErrorActionPreference = "Stop"

$targetDirectory = Join-Path $PSScriptRoot "..\Resources\Raw\Models"
$targetDirectory = [System.IO.Path]::GetFullPath($targetDirectory)

New-Item `
    -ItemType Directory `
    -Path $targetDirectory `
    -Force | Out-Null

Write-Host "Installiere bzw. aktualisiere huggingface_hub..."

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"

& python -m pip install --upgrade huggingface_hub 2>&1 |
    ForEach-Object { Write-Host $_ }

$pipExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference

if ($pipExitCode -ne 0)
{
    throw "huggingface_hub konnte nicht installiert werden. Exitcode: $pipExitCode"
}

$env:QWEN_MODEL_TARGET = $targetDirectory

$pythonScript = @'
import os
import sys
from huggingface_hub import hf_hub_download

repo_id = "Qwen/Qwen2.5-1.5B-Instruct-GGUF"
filename = "qwen2.5-1.5b-instruct-q4_k_m.gguf"
target_directory = os.environ["QWEN_MODEL_TARGET"]

print(f"Lade {filename} herunter...")
print(f"Zielordner: {target_directory}")

try:
    downloaded_path = hf_hub_download(
        repo_id=repo_id,
        filename=filename,
        local_dir=target_directory
    )

    print(f"Modell erfolgreich gespeichert: {downloaded_path}")
except Exception as exc:
    print(f"Download fehlgeschlagen: {exc}", file=sys.stderr)
    sys.exit(1)
'@

$ErrorActionPreference = "Continue"

$pythonScript | python - 2>&1 |
    ForEach-Object { Write-Host $_ }

$downloadExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference

if ($downloadExitCode -ne 0)
{
    throw "Das Qwen-Modell konnte nicht heruntergeladen werden. Exitcode: $downloadExitCode"
}

$modelPath = Join-Path `
    $targetDirectory `
    "qwen2.5-1.5b-instruct-q4_k_m.gguf"

if (-not (Test-Path $modelPath))
{
    throw "Der Download wurde beendet, aber die Modelldatei wurde nicht gefunden: $modelPath"
}

$modelFile = Get-Item $modelPath
$modelSizeMb = [Math]::Round($modelFile.Length / 1MB, 2)

Write-Host ""
Write-Host "Download abgeschlossen."
Write-Host "Datei: $($modelFile.FullName)"
Write-Host "Groesse: $modelSizeMb MB"