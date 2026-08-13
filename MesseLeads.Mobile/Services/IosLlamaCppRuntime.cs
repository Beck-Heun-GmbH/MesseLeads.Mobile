#if IOS
using System.Runtime.InteropServices;
using System.Text;

namespace MesseLeads.Mobile.Services;

public sealed class IosLlamaCppRuntime : ILocalLlmRuntime
{
    private const string NativeLibraryName = "@rpath/MesseLeadsLlamaBridge.framework/MesseLeadsLlamaBridge";
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private Exception? _availabilityError;

    public bool IsAvailable
    {
        get
        {
            try
            {
                var apiVersion = NativeMethods.GetApiVersion();
                if (apiVersion == 1)
                {
                    _availabilityError = null;
                    return true;
                }

                _availabilityError = new InvalidOperationException(
                    $"MesseLeadsLlamaBridge meldet API-Version {apiVersion}, erwartet wurde 1.");

                return false;
            }
            catch (Exception ex)
            {
                _availabilityError = ex;
                return false;
            }
        }
    }

    public string? AvailabilityError
    {
        get
        {
            _ = IsAvailable;
            return _availabilityError is null
                ? null
                : $"{_availabilityError.GetType().Name}: {_availabilityError.Message}";
        }
    }

    public async Task<string> GenerateAsync(
        string modelPath,
        string prompt,
        int contextSize,
        int maxOutputTokens,
        int threadCount,
        float temperature,
        float topP,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                "Die lokale GGUF-Modelldatei wurde nicht gefunden.",
                modelPath);
        }

        if (!IsAvailable)
        {
            throw new DllNotFoundException(
                AvailabilityError ??
                "MesseLeadsLlamaBridge.framework konnte nicht geladen werden.");
        }

        await _runLock.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(
                () => GenerateCore(
                    modelPath,
                    prompt,
                    contextSize,
                    maxOutputTokens,
                    threadCount,
                    temperature,
                    topP),
                cancellationToken);
        }
        finally
        {
            _runLock.Release();
        }
    }

    private static string GenerateCore(
        string modelPath,
        string prompt,
        int contextSize,
        int maxOutputTokens,
        int threadCount,
        float temperature,
        float topP)
    {
        const int outputCapacity = 64 * 1024;
        var output = new byte[outputCapacity];

        var result = NativeMethods.Generate(
            modelPath,
            prompt,
            Math.Max(512, contextSize),
            Math.Max(32, maxOutputTokens),
            Math.Max(1, threadCount),
            temperature,
            topP,
            output,
            output.Length);

        if (result < 0)
        {
            var errorBuffer = new byte[4096];
            NativeMethods.GetLastError(errorBuffer, errorBuffer.Length);
            var error = DecodeZeroTerminated(errorBuffer);

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"llama.cpp wurde mit Fehlercode {result} beendet."
                    : error);
        }

        return Encoding.UTF8
            .GetString(output, 0, Math.Min(result, output.Length))
            .Trim();
    }

    private static string DecodeZeroTerminated(byte[] buffer)
    {
        var length = Array.IndexOf(buffer, (byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        return Encoding.UTF8.GetString(buffer, 0, length).Trim();
    }

    private static class NativeMethods
    {
        [DllImport(
            NativeLibraryName,
            EntryPoint = "ml_llama_get_api_version",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetApiVersion();

        [DllImport(
            NativeLibraryName,
            EntryPoint = "ml_llama_generate",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Generate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt,
            int contextSize,
            int maxOutputTokens,
            int threadCount,
            float temperature,
            float topP,
            [Out] byte[] output,
            int outputCapacity);

        [DllImport(
            NativeLibraryName,
            EntryPoint = "ml_llama_get_last_error",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetLastError(
            [Out] byte[] output,
            int outputCapacity);
    }
}
#endif
