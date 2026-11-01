using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Infrastructure.FileStorage;

/// <summary>
/// Stores legal documents under ContentRoot/private-files/dossiers (never under wwwroot).
/// Private URL format: /api/dossiers/{folder}/{ownerUserId}/{key}; the file is read through
/// DossierFilesController, which checks ownership or a back-office role.
/// </summary>
public sealed class DossierStorage(IWebHostEnvironment environment) : IDossierStorage
{
    public const string UrlPrefix = "/api/dossiers/";

    private string Root => Path.Combine(environment.ContentRootPath, "private-files", "dossiers");

    public async Task<string> SaveAsync(Stream content, string extension, string folder, long ownerUserId,
        CancellationToken cancellationToken)
    {
        string directory = Path.Combine(Root, folder, ownerUserId.ToString());
        Directory.CreateDirectory(directory);
        string key = Guid.NewGuid().ToString("N") + NormalizeExtension(extension);
        await WriteAsync(Path.Combine(directory, key), content, cancellationToken);
        return $"{UrlPrefix}{folder}/{ownerUserId}/{key}";
    }

    /// <summary>Resolves a private URL to a file path, or null when the URL is not a stored dossier.</summary>
    public string? ResolvePath(string folder, long ownerUserId, string key)
    {
        if (!IsSafeKey(key) || !IsSafeKey(folder)) return null;
        string path = Path.Combine(Root, folder, ownerUserId.ToString(), key);
        return File.Exists(path) ? path : null;
    }

    public static string ContentType(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        _ => "image/jpeg"
    };

    private static async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken)
    {
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
    }

    private static string NormalizeExtension(string extension)
    {
        string ext = extension.ToLowerInvariant();
        return ext == ".jpeg" ? ".jpg" : ext;
    }

    private static bool IsSafeKey(string value) =>
        !string.IsNullOrWhiteSpace(value) && value == Path.GetFileName(value)
        && !value.Contains("..", StringComparison.Ordinal);
}

/// <summary>Checks the real signature of an uploaded document (PDF, JPG or PNG, at most 5 MB).</summary>
public static class DocumentFileCheck
{
    public const int MaximumBytes = 5 * 1024 * 1024;

    /// <summary>Returns null when the file is valid, otherwise the Vietnamese error message.</summary>
    public static async Task<string?> ValidateAsync(
        IFormFile? file, string[] allowedExtensions, CancellationToken cancellationToken)
    {
        string allowed = string.Join(", ", allowedExtensions.Select(e => e.TrimStart('.').ToUpperInvariant()));
        if (file is null || file.Length <= 0)
            return $"Vui lòng chọn tệp ({allowed}, tối đa 5 MB).";
        if (file.Length > MaximumBytes)
            return "Tệp vượt quá 5 MB. Vui lòng chọn tệp nhỏ hơn.";

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return $"Chỉ hỗ trợ tệp {allowed}.";

        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        int read = await stream.ReadAsync(header, cancellationToken);
        bool png = read == 8 && header.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        bool pdf = read >= 5 && System.Text.Encoding.ASCII.GetString(header, 0, 5) == "%PDF-";
        bool ok = extension switch
        {
            ".pdf" => pdf,
            ".png" => png,
            _ => jpeg
        };
        return ok ? null : "Tệp bị lỗi hoặc nội dung không đúng định dạng.";
    }
}
