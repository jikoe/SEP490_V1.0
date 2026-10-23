using System;
using System.Collections.Generic;

namespace AgriSustain.Infrastructure.Data.Entities;

public partial class CultivationLog
{
    public long CultivationLogId { get; set; }

    public long CropSeasonId { get; set; }

    public long RecordedByUserId { get; set; }

    public DateTime ActionDate { get; set; }

    public string ActionType { get; set; } = null!;

    public string? DetailDescription { get; set; }

    public string? InputMaterial { get; set; }

    public string? Dosage { get; set; }

    public string? PerformedBy { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual CropSeason CropSeason { get; set; } = null!;

    public virtual User RecordedByUser { get; set; } = null!;
}
