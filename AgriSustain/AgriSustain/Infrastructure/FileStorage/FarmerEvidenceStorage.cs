using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;

namespace AgriSustain.Infrastructure.FileStorage;

public sealed class FarmerEvidenceStorage(IWebHostEnvironment environment) : IFarmerEvidenceStorage
{
    private string Folder(long actor) =>
        Path.Combine(environment.ContentRootPath, "private-farm-evidence", actor.ToString());

    public async Task<IReadOnlyList<string>> SaveImagesAsync(
        long actor,
        IReadOnlyList<IFormFile> files,
        string feature,
        CancellationToken ct)
    {
        var error = feature == "log" ? FarmManagementMessages.MSG96 : FarmManagementMessages.MSG120;
        FarmState.Require(files.Count is> 0 and <= 20, error, "evidenceUrls");
        // Validate the entire set before storing any file.
        var buffers = new List<(byte[] Content, string Extension)>();
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            FarmState.Require(file.Length is> 0 and <= 5242880 && ext is ".jpg" or ".jpeg" or ".png", error, "evidenceUrls");
            await using var input = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            bool png = bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            bool jpeg = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
            FarmState.Require(bytes.Length <= 5242880 && (ext == ".png" ? png : jpeg), error, "evidenceUrls");
            buffers.Add((bytes, ext == ".jpeg" ? ".jpg" : ext));
        }

        Directory.CreateDirectory(Folder(actor));
        var refs = new List<string>();
        var created = new List<string>();
        try
        {
            foreach (var file in buffers)
            {
                var key = Guid.NewGuid().ToString("N") + file.Extension;
                var path = Path.Combine(Folder(actor), key);
                created.Add(path);
                await File.WriteAllBytesAsync(path, file.Content, ct);
                refs.Add("/api/farmer/evidence/" + key);
            }

            return refs;
        }
        catch
        {
            foreach (var path in created)
                if (File.Exists(path))
                    File.Delete(path);
            throw;
        }
    }


    public string? ResolvePath(long actor, string reference)
    {
        const string prefix = "/api/farmer/evidence/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        string key = reference[prefix.Length..];
        if (key != Path.GetFileName(key) || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) || Path.GetExtension(key)is not (".jpg" or ".png"))
            return null;
        var path = Path.Combine(Folder(actor), key);
        return File.Exists(path) ? path : null;
    }


    public bool Owns(
        long actor,
        string reference) =>
        !string.IsNullOrWhiteSpace(reference) && ResolvePath(actor, reference)is not null;

    public async Task<string?> StoreReferencesAsync(
        long actor,
        IReadOnlyList<string> references,
        CancellationToken ct)
    {
        if (references.Count == 0)
            return null;
        var key = Guid.NewGuid().ToString("N") + ".json";
        Directory.CreateDirectory(Folder(actor));
        await File.WriteAllTextAsync(Path.Combine(Folder(actor), key), JsonSerializer.Serialize(references), ct);
        // One bounded column reference; multiple files do not exceed CultivationLog.ImageUrl(500).
        return "farm-evidence:" + key;
    }


    public IReadOnlyList<string> ReadReferences(long actor, string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return[];
        if (stored.StartsWith("farm-evidence:", StringComparison.Ordinal))
        {
            var key = stored[14..];
            if (key != Path.GetFileName(key) || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) || !key.EndsWith(".json"))
                return[];
            var path = Path.Combine(Folder(actor), key);
            if (!File.Exists(path))
                return[];
            try
            {
                return (JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? []).Where(x => Owns(actor, x)).ToList();
            }
            catch (JsonException)
            {
                return[];
            }
        }

        if (stored.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<string[]>(stored) ?? [];
            }
            catch (JsonException)
            {
                return[];
            }
        }

        return[stored];
    }
}
