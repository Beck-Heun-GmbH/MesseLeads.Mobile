using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LocalLlmModelService
{
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public string GetInstalledModelPath()
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "llm-models");
        return Path.Combine(directory, LocalLlmOptions.ModelFileName);
    }

    public async Task<LocalLlmModelState> GetStateAsync(
        CancellationToken cancellationToken = default)
    {
        var path = GetInstalledModelPath();

        if (!File.Exists(path))
        {
            return new LocalLlmModelState
            {
                IsInstalled = false,
                ModelPath = path,
                StatusText = "Qwen-Modell ist noch nicht lokal installiert."
            };
        }

        var fileInfo = new FileInfo(path);

        return new LocalLlmModelState
        {
            IsInstalled = fileInfo.Length > 0,
            ModelPath = path,
            SizeBytes = fileInfo.Length,
            StatusText = fileInfo.Length > 0
                ? $"Qwen-Modell ist lokal installiert ({FormatBytes(fileInfo.Length)})."
                : "Die lokale Modelldatei ist leer."
        };
    }

    public async Task<LocalLlmModelState> EnsureInstalledFromAppPackageAsync(
        CancellationToken cancellationToken = default)
    {
        await _installLock.WaitAsync(cancellationToken);

        try
        {
            var current = await GetStateAsync(cancellationToken);
            if (current.IsInstalled)
            {
                return current;
            }

            var targetPath = GetInstalledModelPath();
            var targetDirectory = Path.GetDirectoryName(targetPath)
                ?? throw new InvalidOperationException("Das Modellverzeichnis konnte nicht bestimmt werden.");

            Directory.CreateDirectory(targetDirectory);

            try
            {
                await using var source = await FileSystem.OpenAppPackageFileAsync(
                    LocalLlmOptions.PackagedModelPath);
                await using var target = new FileStream(
                    targetPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    useAsync: true);

                await source.CopyToAsync(target, 1024 * 1024, cancellationToken);
            }
            catch (FileNotFoundException ex)
            {
                throw new InvalidOperationException(
                    $"Die Modelldatei '{LocalLlmOptions.PackagedModelPath}' ist nicht im App-Paket enthalten. " +
                    "Lege die GGUF-Datei unter Resources/Raw/Models ab.", ex);
            }

            return await GetStateAsync(cancellationToken);
        }
        finally
        {
            _installLock.Release();
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = megabyte * 1024d;

        return bytes >= gigabyte
            ? $"{bytes / gigabyte:0.00} GB"
            : $"{bytes / megabyte:0} MB";
    }
}
