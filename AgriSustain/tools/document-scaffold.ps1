param([string]$ProjectRoot = (Join-Path $PSScriptRoot '..'))
$ErrorActionPreference = 'Stop'
# Reapply English XML documentation after Database First regeneration. This helper changes
# comments only, preserving all SQL-generated mappings and keeping credentials out of sources.
$contextPath = Join-Path $ProjectRoot 'src/AgriSustain.Infrastructure/Data/Contexts/AgriSustainDbContext.cs'
$source = [IO.File]::ReadAllText($contextPath)
if (-not $source.Contains('/// <summary>Maps the existing AgriSustain SQL schema')) {
    $source = $source.Replace('public partial class AgriSustainDbContext',
        "/// <summary>Maps the existing AgriSustain SQL schema; regenerated with EF Database First.</summary>`npublic partial class AgriSustainDbContext")
    $source = $source.Replace('    public AgriSustainDbContext(',
        "    /// <summary>Receives scoped SQL Server options from dependency injection.</summary>`n    public AgriSustainDbContext(")
    $source = $source.Replace('    protected override void OnModelCreating(',
        "    /// <summary>Configures the database's existing keys, defaults, indexes and relationships.</summary>`n    protected override void OnModelCreating(")
    $source = $source.Replace('    partial void OnModelCreatingPartial(',
        "    /// <summary>Provides a separate-file extension point without editing generated mappings.</summary>`n    partial void OnModelCreatingPartial(")
    [IO.File]::WriteAllText($contextPath, $source.Replace("`r`n", "`n"))
}
