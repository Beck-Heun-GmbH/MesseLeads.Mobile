#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using UIKit;
#endif
#if ANDROID
using AndroidBitmap = Android.Graphics.Bitmap;
using AndroidBitmapFactory = Android.Graphics.BitmapFactory;
#endif
using MesseLeads.Mobile.Data;
using MesseLeads.Mobile.Models;

namespace MesseLeads.Mobile.Services;

public sealed class LocalLeadImageService
{
    private const int MaxUploadImageEdge = 1800;
    private const float UploadJpegQuality = 0.78f;
    private const double MinCropFraction = 0.05d;
    private readonly LocalDatabaseService _databaseService;

    public LocalLeadImageService(LocalDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<LocalLeadImage>> GetPendingUploadForLeadAsync(Guid leadLocalId)
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLeadImage>()
            .Where(x => x.LeadLocalId == leadLocalId && x.SyncStatus == "Pending")
            .OrderBy(x => x.CreatedUtc)
            .ToListAsync();
    }

    public async Task<LocalLeadImage?> GetLatestBusinessCardImageAsync(Guid leadLocalId)
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLeadImage>()
            .Where(x => x.LeadLocalId == leadLocalId && x.ImageType == "BusinessCard")
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<LocalLeadImage?> AddBusinessCardPhotoAsync(Guid leadLocalId)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
        {
            throw new InvalidOperationException("Kameraberechtigung wurde nicht erteilt.");
        }

        FileResult? photo;

        try
        {
            photo = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Visitenkarte fotografieren"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Kamera konnte nicht geöffnet werden. Im Emulator ist häufig keine Kamera konfiguriert. Teste alternativ auf einem echten Gerät oder aktiviere im Android Emulator die Webcam. Details: " + ex.Message,
                ex);
        }

        if (photo is null)
        {
            return null;
        }

        return await SaveImageAsync(leadLocalId, photo, "BusinessCard");
    }

    public async Task<LocalLeadImage?> PickBusinessCardPhotoAsync(Guid leadLocalId)
    {
        var photos = await MediaPicker.PickPhotosAsync(new MediaPickerOptions
        {
            Title = "Visitenkarte auswählen"
        });

        // Der Wizard verarbeitet genau ein Visitenkartenbild.
        var photo = photos?.FirstOrDefault();

        if (photo is null)
        {
            return null;
        }

        return await SaveImageAsync(leadLocalId, photo, "BusinessCard");
    }

    private async Task<LocalLeadImage> SaveImageAsync(Guid leadLocalId, FileResult photo, string imageType)
    {
        var imagesDir = Path.Combine(FileSystem.AppDataDirectory, "lead-images");
        Directory.CreateDirectory(imagesDir);

        var fileName = $"{leadLocalId:N}_{Guid.NewGuid():N}.jpg";
        var targetPath = Path.Combine(imagesDir, fileName);

        await using (var sourceStream = await photo.OpenReadAsync())
        {
            await SaveOptimizedImageAsync(sourceStream, targetPath);
        }

        var image = new LocalLeadImage
        {
            LocalImageId = Guid.NewGuid(),
            LeadLocalId = leadLocalId,
            LocalFilePath = targetPath,
            ImageType = imageType,
            SyncStatus = "Pending",
            CreatedUtc = DateTime.UtcNow
        };

        var db = await _databaseService.GetDatabaseAsync();
        await db.InsertAsync(image);

        return image;
    }

    public async Task<List<LocalLeadImage>> GetForLeadAsync(Guid leadLocalId)
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLeadImage>()
            .Where(x => x.LeadLocalId == leadLocalId)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync();
    }

    public async Task<List<LocalLeadImage>> GetPendingUploadAsync()
    {
        var db = await _databaseService.GetDatabaseAsync();

        return await db.Table<LocalLeadImage>()
            .Where(x => x.SyncStatus == "Pending")
            .OrderBy(x => x.CreatedUtc)
            .ToListAsync();
    }

    public async Task MarkSyncedAsync(LocalLeadImage image, int serverImageId)
    {
        image.ServerImageId = serverImageId;
        image.SyncStatus = "Synced";

        var db = await _databaseService.GetDatabaseAsync();
        await db.UpdateAsync(image);

        if (File.Exists(image.LocalFilePath))
        {
            File.Delete(image.LocalFilePath);
        }
    }

    public async Task MarkMissingAsync(LocalLeadImage image)
    {
        image.SyncStatus = "Missing";

        var db = await _databaseService.GetDatabaseAsync();
        await db.UpdateAsync(image);
    }

    public async Task CropAsync(
        LocalLeadImage image,
        double left,
        double top,
        double right,
        double bottom)
    {
#if IOS || MACCATALYST || ANDROID
        if (!File.Exists(image.LocalFilePath))
        {
            throw new FileNotFoundException("Das Foto wurde lokal nicht gefunden.", image.LocalFilePath);
        }

        var crop = NormalizeCropBounds(left, top, right, bottom);
        var directory = Path.GetDirectoryName(image.LocalFilePath) ?? FileSystem.AppDataDirectory;
        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.crop.jpg");

        try
        {
#if IOS || MACCATALYST
            CropWithUIKit(image.LocalFilePath, tempPath, crop.Left, crop.Top, crop.Right, crop.Bottom);
#elif ANDROID
            CropWithAndroidBitmap(image.LocalFilePath, tempPath, crop.Left, crop.Top, crop.Right, crop.Bottom);
#endif

            File.Move(tempPath, image.LocalFilePath, true);
            image.SyncStatus = "Pending";

            var db = await _databaseService.GetDatabaseAsync();
            await db.UpdateAsync(image);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
#else
        await Task.CompletedTask;
#endif
    }

    public async Task OptimizeExistingImageForUploadAsync(LocalLeadImage image)
    {
#if IOS || MACCATALYST
        if (!File.Exists(image.LocalFilePath))
        {
            return;
        }

        var fileInfo = new FileInfo(image.LocalFilePath);
        var extension = Path.GetExtension(image.LocalFilePath);

        if ((extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) &&
            fileInfo.Length <= 5_000_000)
        {
            return;
        }

        var directory = Path.GetDirectoryName(image.LocalFilePath) ?? FileSystem.AppDataDirectory;
        var optimizedPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(image.LocalFilePath)}.jpg");
        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.upload.jpg");

        await using (var sourceStream = File.OpenRead(image.LocalFilePath))
        {
            await SaveOptimizedImageAsync(sourceStream, tempPath);
        }

        if (!string.Equals(image.LocalFilePath, optimizedPath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(image.LocalFilePath))
        {
            File.Delete(image.LocalFilePath);
        }

        File.Move(tempPath, optimizedPath, true);
        image.LocalFilePath = optimizedPath;

        var db = await _databaseService.GetDatabaseAsync();
        await db.UpdateAsync(image);
#else
        await Task.CompletedTask;
#endif
    }

    public async Task DeleteAsync(LocalLeadImage image)
    {
        var db = await _databaseService.GetDatabaseAsync();

        if (File.Exists(image.LocalFilePath))
        {
            File.Delete(image.LocalFilePath);
        }

        await db.DeleteAsync(image);
    }

    private static async Task SaveOptimizedImageAsync(Stream sourceStream, string targetPath)
    {
#if IOS || MACCATALYST
        using var buffer = new MemoryStream();
        await sourceStream.CopyToAsync(buffer);

        using var data = NSData.FromArray(buffer.ToArray());
        using var sourceImage = UIImage.LoadFromData(data)
            ?? throw new InvalidOperationException("Das Foto konnte nicht als Bild geladen werden.");
        using var resizedImage = ResizeForUpload(sourceImage);
        using var jpegData = resizedImage.AsJPEG((nfloat)UploadJpegQuality)
            ?? throw new InvalidOperationException("Das Foto konnte nicht als JPEG gespeichert werden.");

        await File.WriteAllBytesAsync(targetPath, jpegData.ToArray());
#else
        await using var targetStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(targetStream);
#endif
    }

    private static (double Left, double Top, double Right, double Bottom) NormalizeCropBounds(
        double left,
        double top,
        double right,
        double bottom)
    {
        (left, right) = NormalizeRange(left, right);
        (top, bottom) = NormalizeRange(top, bottom);

        return (left, top, right, bottom);
    }

    private static (double Start, double End) NormalizeRange(double start, double end)
    {
        start = Math.Clamp(start, 0d, 1d);
        end = Math.Clamp(end, 0d, 1d);

        if (end < start)
        {
            (start, end) = (end, start);
        }

        if (end - start >= MinCropFraction)
        {
            return (start, end);
        }

        var center = (start + end) / 2d;
        start = center - MinCropFraction / 2d;
        end = center + MinCropFraction / 2d;

        if (start < 0d)
        {
            end -= start;
            start = 0d;
        }

        if (end > 1d)
        {
            start -= end - 1d;
            end = 1d;
        }

        return (Math.Clamp(start, 0d, 1d), Math.Clamp(end, 0d, 1d));
    }

#if IOS || MACCATALYST
    private static void CropWithUIKit(
        string sourcePath,
        string targetPath,
        double left,
        double top,
        double right,
        double bottom)
    {
        using var sourceImage = UIImage.FromFile(sourcePath)
            ?? throw new InvalidOperationException("Das Foto konnte nicht als Bild geladen werden.");
        using var sourceCgImage = sourceImage.CGImage
            ?? throw new InvalidOperationException("Das Foto konnte nicht zugeschnitten werden.");

        var cropRect = CreatePixelCropRect(
            (int)sourceCgImage.Width,
            (int)sourceCgImage.Height,
            left,
            top,
            right,
            bottom);

        using var croppedCgImage = sourceCgImage.WithImageInRect(cropRect)
            ?? throw new InvalidOperationException("Der gewählte Bildbereich konnte nicht erstellt werden.");
        using var croppedImage = UIImage.FromImage(croppedCgImage, 1, UIImageOrientation.Up);
        using var resizedImage = ResizeForUpload(croppedImage);
        using var jpegData = resizedImage.AsJPEG((nfloat)UploadJpegQuality)
            ?? throw new InvalidOperationException("Das zugeschnittene Foto konnte nicht gespeichert werden.");

        File.WriteAllBytes(targetPath, jpegData.ToArray());
    }

    private static CGRect CreatePixelCropRect(
        int imageWidth,
        int imageHeight,
        double left,
        double top,
        double right,
        double bottom)
    {
        var x = Math.Clamp((int)Math.Round(imageWidth * left), 0, imageWidth - 1);
        var y = Math.Clamp((int)Math.Round(imageHeight * top), 0, imageHeight - 1);
        var width = Math.Clamp((int)Math.Round(imageWidth * (right - left)), 1, imageWidth - x);
        var height = Math.Clamp((int)Math.Round(imageHeight * (bottom - top)), 1, imageHeight - y);

        return new CGRect(x, y, width, height);
    }

    private static UIImage ResizeForUpload(UIImage image)
    {
        var width = image.Size.Width;
        var height = image.Size.Height;
        var longestEdge = Math.Max(width, height);
        var scale = longestEdge > MaxUploadImageEdge
            ? MaxUploadImageEdge / longestEdge
            : 1d;

        var targetSize = new CGSize(
            Math.Max(1, width * scale),
            Math.Max(1, height * scale));

        var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Opaque = true;
        format.Scale = (nfloat)1;

        using var renderer = new UIGraphicsImageRenderer(targetSize, format);

        return renderer.CreateImage(_ =>
        {
            image.Draw(new CGRect(0, 0, targetSize.Width, targetSize.Height));
        });
    }
#endif

#if ANDROID
    private static void CropWithAndroidBitmap(
        string sourcePath,
        string targetPath,
        double left,
        double top,
        double right,
        double bottom)
    {
        using var sourceBitmap = AndroidBitmapFactory.DecodeFile(sourcePath)
            ?? throw new InvalidOperationException("Das Foto konnte nicht als Bild geladen werden.");

        var x = Math.Clamp((int)Math.Round(sourceBitmap.Width * left), 0, sourceBitmap.Width - 1);
        var y = Math.Clamp((int)Math.Round(sourceBitmap.Height * top), 0, sourceBitmap.Height - 1);
        var width = Math.Clamp((int)Math.Round(sourceBitmap.Width * (right - left)), 1, sourceBitmap.Width - x);
        var height = Math.Clamp((int)Math.Round(sourceBitmap.Height * (bottom - top)), 1, sourceBitmap.Height - y);

        using var croppedBitmap = AndroidBitmap.CreateBitmap(sourceBitmap, x, y, width, height);
        using var output = File.Create(targetPath);
        var jpegFormat = AndroidBitmap.CompressFormat.Jpeg
            ?? throw new InvalidOperationException("JPEG-Komprimierung ist auf diesem Gerät nicht verfügbar.");

        if (!croppedBitmap.Compress(jpegFormat, (int)(UploadJpegQuality * 100), output))
        {
            throw new InvalidOperationException("Das zugeschnittene Foto konnte nicht gespeichert werden.");
        }
    }
#endif
}
