using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Domain.Entities;
using QRCoder;

namespace AgriSustain.Application.Services;

public sealed class FarmerTraceabilityLabelService
{
    public FarmLabelResponse RenderLabel(HarvestBatch batch, string message)
    {
        var qr = batch.TraceabilityQrcode ?? throw new InvalidOperationException("QR identity is missing.");
        using var generator = new QRCodeGenerator();
        using var matrix = generator.CreateQrCode(qr.TargetUrl, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(matrix);
        using var svg = new SvgQRCode(matrix);
        return new FarmLabelResponse(batch.HarvestBatchId, batch.BatchCode, batch.Product.ProductName, batch.Product.FarmerProfile.FarmName, batch.CropSeason.SeasonName, batch.ActualHarvestDate, batch.ExpirationDate, qr.GetExistingIdentity(), qr.TargetUrl, "data:image/png;base64," + Convert.ToBase64String(png.GetGraphic(8)), svg.GetGraphic(8), message);
    }
}
