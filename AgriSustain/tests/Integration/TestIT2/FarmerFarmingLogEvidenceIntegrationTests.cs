using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;
using AgriSustain.Infrastructure.FileStorage;

namespace AgriSustain.TestIT2.Integration;

[Trait("Layer", "Integration")]
public sealed class FarmerFarmingLogEvidenceIntegrationTests
{
    [Fact]
    public async Task IT_FAR_02_FILE_001_ValidImage()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerEvidenceStorage(folder);
        var refs = await storage.SaveImagesAsync(101, [PrivateEvidenceDirectory.File([255, 216, 255, 217], "proof.jpg")], "log", default);
        string reference = Assert.Single(refs);
        Assert.True(storage.Owns(101, reference));
        Assert.False(storage.Owns(102, reference));
        Assert.NotNull(storage.ResolvePath(101, reference));
        var manifest = await storage.StoreReferencesAsync(101, refs, default);
        Assert.Equal(refs, storage.ReadReferences(101, manifest));
    }


    [Fact]
    public async Task IT_FAR_02_FILE_002_WrongExtension()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerEvidenceStorage(folder);
        var error = await Assert.ThrowsAsync<FarmRuleException>(() => storage.SaveImagesAsync(101, [PrivateEvidenceDirectory.File([255, 216, 255, 217], "proof.pdf")], "log", default));
        Assert.Equal(FarmManagementMessages.MSG96, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_02_FILE_003_SpoofedHeader()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerEvidenceStorage(folder);
        var error = await Assert.ThrowsAsync<FarmRuleException>(() => storage.SaveImagesAsync(101, [PrivateEvidenceDirectory.File([1, 2, 3, 4], "proof.jpg")], "log", default));
        Assert.Equal(FarmManagementMessages.MSG96, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_02_FILE_004_Empty()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerEvidenceStorage(folder);
        var error = await Assert.ThrowsAsync<FarmRuleException>(() => storage.SaveImagesAsync(101, [PrivateEvidenceDirectory.File([], "proof.jpg")], "log", default));
        Assert.Equal(FarmManagementMessages.MSG96, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_02_FILE_005_Oversized()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerEvidenceStorage(folder);
        var error = await Assert.ThrowsAsync<FarmRuleException>(() => storage.SaveImagesAsync(101, [PrivateEvidenceDirectory.File(new byte[5242881], "proof.png")], "log", default));
        Assert.Equal(FarmManagementMessages.MSG96, error.Message);
        Assert.Equal(0, folder.Count);
    }


    [Fact]
    public async Task IT_FAR_02_FILE_006_WholeSetValidation()
    {
        using var folder = new PrivateEvidenceDirectory();
        var storage = new FarmerEvidenceStorage(folder);
        await Assert.ThrowsAsync<FarmRuleException>(() => storage.SaveImagesAsync(101, [PrivateEvidenceDirectory.File([255, 216, 255, 217], "good.jpg"), PrivateEvidenceDirectory.File([1, 2, 3, 4], "bad.jpg")], "log", default));
        Assert.Equal(0, folder.Count);
    }
    [Fact]
    public async Task IT_FAR_02_FILE_007_MaximumImageSize()
    {
        using var folder = new PrivateEvidenceDirectory();
        var bytes = new byte[5242880];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        var storage = new FarmerEvidenceStorage(folder);
        string reference = Assert.Single(await storage.SaveImagesAsync(101, [PrivateEvidenceDirectory.File(bytes, "proof.png")], "log", default));
        Assert.Equal(5242880, new FileInfo(storage.ResolvePath(101, reference)!).Length);
    }
}
