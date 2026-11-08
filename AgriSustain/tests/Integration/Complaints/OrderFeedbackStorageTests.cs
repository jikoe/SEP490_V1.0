using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Infrastructure.FileStorage;
using AgriSustain.TestIT2.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.IntegrationTests.Complaints;

public sealed class OrderFeedbackStorageTests
{
    [Fact]
    public async Task SaveAsync_Mp4_PreservesBytesAndResolvesOnlyForOwner()
    {
        using var directory = new PrivateEvidenceDirectory();
        var storage = new OrderFeedbackStorage(directory, NullLogger<OrderFeedbackStorage>.Instance);
        var complaintFiles = new FarmerComplaintFileStorage(directory);
        var bytes = Convert.FromHexString("000000146674797069736F6D000000006D703432000000086D646174");
        using var input = new MemoryStream(bytes);

        string url = await storage.SaveAsync(20, new FeedbackFile("evidence.MP4", bytes.Length, input), default);
        string key = Path.GetFileName(url);
        Assert.Null(complaintFiles.Resolve(21, url));
        Assert.Null(storage.OpenEvidence(21, key));
        Assert.NotNull(complaintFiles.Resolve(20, url));
        Assert.False(await complaintFiles.IsValidOwnedAsync(20, url, default));

        var download = Assert.IsType<FeedbackDownload>(storage.OpenEvidence(20, key));
        await using (download.Content)
        {
            using var saved = new MemoryStream();
            await download.Content.CopyToAsync(saved);
            Assert.Equal(bytes, saved.ToArray());
            Assert.Equal("video/mp4", download.ContentType);
        }
        storage.Delete(20, [url]);
        Assert.Equal(0, directory.Count);
    }

    [Theory]
    [InlineData("3C68746D6C3E")] // HTML renamed to .mp4
    [InlineData("0000001466747970617669660000000061766966000000086D646174")] // AVIF
    public async Task SaveAsync_MislabeledMp4_RejectsWithoutWriting(string hex)
    {
        using var directory = new PrivateEvidenceDirectory();
        var storage = new OrderFeedbackStorage(directory, NullLogger<OrderFeedbackStorage>.Instance);
        var bytes = Convert.FromHexString(hex);
        using var input = new MemoryStream(bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            storage.SaveAsync(20, new FeedbackFile("evidence.mp4", bytes.Length, input), default));

        Assert.Equal(0, directory.Count);
    }
}
