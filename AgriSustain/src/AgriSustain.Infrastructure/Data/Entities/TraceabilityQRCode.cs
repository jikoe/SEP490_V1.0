using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class TraceabilityQRCode
{
    public long TraceabilityQRCodeId { get; set; }

    public long HarvestBatchId { get; set; }

    public string QRContentString { get; set; } = null!;

    public string TargetUrl { get; set; } = null!;

    public int ScanCount { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime GeneratedAt { get; set; }

    public virtual HarvestBatch HarvestBatch { get; set; } = null!;
}
