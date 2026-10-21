using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class TraceabilityQrcode
{
    public long TraceabilityQrcodeId { get; set; }

    public long HarvestBatchId { get; set; }

    public string QrcontentString { get; set; } = null!;

    public string TargetUrl { get; set; } = null!;

    public int ScanCount { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime GeneratedAt { get; set; }

    public virtual HarvestBatch HarvestBatch { get; set; } = null!;
}
