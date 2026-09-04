using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LocalLlmModelService
{
    private readonly SemaphoreSlim _installLock = new(1, 1);

    /// <summary>
    /// Pfad, unter dem das Modell zur Laufzeit gelesen wird.
    /// </summary>
    /// <remarks>
    /// Auf iOS liegt das GGUF als echte Datei im App-Bundle.
    /// llama.cpp braucht nur einen Dateipfad, also wird direkt aus dem Bundle
    /// gelesen. Eine Kopie ins Datenverzeichnis wuerde die 1,12 GB ein zweites
    /// Mal belegen (~2,24 GB auf dem Geraet) und als wiederherstellbare Datei
    /// zusaetzlich in das iCloud-Backup wandern, was Apples Data Storage
    /// Guidelines widerspricht.
    ///
    /// Auf Android steckt das Asset komprimiert im APK und ist kein regulaerer
    /// Dateipfad; dort und auf Mac Catalyst (abweichendes Bundle-Layout)
    /// bleibt das Entpacken ins Datenverzeichnis notwendig.
    /// </remarks>
    public string GetInstalledModelPath()
    {
        var bundlePath = TryGetBundleModelPath();
        if (bundlePath is not null)
        {
            return bundlePath;
        }

        var directory = Path.Combine(FileSystem.AppDataDirectory, "llm-models");
        return Path.Combine(directory, LocalLlmOptions.ModelFileName);
    }

    /// <summary>
    /// Liefert den Bundle-Pfad, sofern die Plattform das Asset als lesbare
    /// Datei ablegt, sonst <c>null</c>.
    /// </summary>
    private static string? TryGetBundleModelPath()
    {
#if IOS
        var candidate = Path.Combine(
            AppContext.BaseDirectory,
            LocalLlmOptions.PackagedModelPath.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(candidate) ? candidate : null;
#else
        return null;
#endif
    }

    public Task<LocalLlmModelState> GetStateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetInstalledModelPath();

        if (!File.Exists(path))
        {
            return Task.FromResult(new LocalLlmModelState
            {
                IsInstalled = false,
                ModelPath = path,
                StatusText = "Qwen-Modell ist noch nicht lokal installiert."
            });
        }

        var fileInfo = new FileInfo(path);

        return Task.FromResult(new LocalLlmModelState
        {
            IsInstalled = fileInfo.Length > 0,
            ModelPath = path,
            SizeBytes = fileInfo.Length,
            StatusText = fileInfo.Length > 0
                ? $"Qwen-Modell ist lokal installiert ({FormatBytes(fileInfo.Length)})."
                : "Die lokale Modelldatei ist leer."
        });
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
