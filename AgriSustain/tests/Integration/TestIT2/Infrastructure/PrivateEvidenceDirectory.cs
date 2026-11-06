using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using AgriSustain.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace AgriSustain.TestIT2.Infrastructure;

internal sealed class PrivateEvidenceDirectory : IWebHostEnvironment, IDisposable
{
    private readonly string id = "AgriSustain_TestIT2_Files_" + Guid.NewGuid().ToString("N");

    public string EnvironmentName { get; set; } = "Testing";

    public string ApplicationName { get; set; } = "TestIT2";

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = "";

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();


    public PrivateEvidenceDirectory()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), id);
        Directory.CreateDirectory(ContentRootPath);
    }


    public static FormFile File(
        byte[] bytes,
        string name) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    public int Count => Directory.GetFiles(ContentRootPath, "*", SearchOption.AllDirectories).Length;


    public void Dispose()
    {
        string expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), id));
        if (Path.GetFullPath(ContentRootPath) != expected || !id.StartsWith("AgriSustain_TestIT2_Files_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing non-test file cleanup.");
        Directory.Delete(expected, recursive: true);
    }
}
