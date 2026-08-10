using System.Security.Cryptography;
using System.Text;

namespace CodexHpBar.Core;

public static class MascotAssetStorage
{
    public const string DirectoryName = "MascotAssets";
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    public static MascotSettings EnsureLocalCopy(MascotSettings settings, string? executionDirectory = null)
    {
        var normalized = settings.Normalize();
        if (normalized.Mode == MascotAssetMode.BuiltInMushroom) return normalized;
        if (string.IsNullOrWhiteSpace(normalized.FilePath))
        {
            throw new InvalidDataException("請先選擇圖片檔案。");
        }

        var sourcePath = Path.GetFullPath(normalized.FilePath);
        var sourceInfo = ValidateSource(sourcePath, normalized.Mode);
        var applicationDirectory = string.IsNullOrWhiteSpace(executionDirectory)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(executionDirectory);
        var storageDirectory = Path.Combine(applicationDirectory, DirectoryName);
        Directory.CreateDirectory(storageDirectory);

        if (IsWithinDirectory(sourcePath, storageDirectory))
        {
            return normalized with { FilePath = sourcePath };
        }

        var hash = ComputeHash(sourcePath);
        var safeStem = CreateSafeFileStem(sourcePath);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var destinationPath = Path.Combine(storageDirectory, $"{hash[..16]}-{safeStem}{extension}");
        if (!File.Exists(destinationPath) || new FileInfo(destinationPath).Length != sourceInfo.Length)
        {
            CopyAtomically(sourcePath, destinationPath);
        }

        return normalized with { FilePath = destinationPath };
    }

    private static FileInfo ValidateSource(string sourcePath, MascotAssetMode mode)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("找不到指定的圖片檔案。", sourcePath);

        var extensions = GetAllowedExtensions(mode);
        var extension = Path.GetExtension(sourcePath);
        if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{mode} 只接受 {string.Join("、", extensions)} 檔案。");
        }

        var sourceInfo = new FileInfo(sourcePath);
        if (sourceInfo.Length > MaxFileSizeBytes)
        {
            throw new InvalidDataException("圖片檔案不可超過 20 MB。");
        }

        return sourceInfo;
    }

    private static string[] GetAllowedExtensions(MascotAssetMode mode) => mode switch
    {
        MascotAssetMode.StaticImage => [".png", ".jpg", ".jpeg", ".bmp", ".ico"],
        MascotAssetMode.AnimatedGif => [".gif"],
        MascotAssetMode.SpriteSheet4x4 => [".png", ".jpg", ".jpeg", ".bmp"],
        _ => []
    };

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string CreateSafeFileStem(string path)
    {
        var originalStem = Path.GetFileNameWithoutExtension(path);
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(Math.Min(originalStem.Length, 80));
        foreach (var character in originalStem)
        {
            builder.Append(character is '\0' || char.IsControl(character) || invalidCharacters.Contains(character)
                ? '_'
                : character);
        }

        var stem = builder.ToString().Trim().TrimEnd('.');
        if (stem.Length == 0) stem = "mascot";
        return stem.Length > 80 ? stem[..80] : stem;
    }

    private static bool IsWithinDirectory(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyAtomically(string sourcePath, string destinationPath)
    {
        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: false);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
