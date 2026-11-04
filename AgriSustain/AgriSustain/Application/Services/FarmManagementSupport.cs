using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public static class FarmState
{
    public static readonly string[] Stages = ["Planned", "Planting", "Growing", "Harvesting", "Completed", "Cancelled", "Crop Failure"];

    public static readonly string[] Activities = ["Tưới nước", "Bón phân", "Phòng trừ sâu bệnh", "Gieo trồng", "Thu hoạch"];

    public static string Stage(string value) => value == "Planning" ? "Planned" : value;

    public static bool Closed(string state) =>
        state is "Completed" or "Cancelled" or "Crop Failure" or "CropFailure";

    public static bool ActiveSeason(string state) => Stage(state)is "Planting" or "Growing" or "Harvesting";

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    private static readonly JsonSerializerOptions StampOptions = new()
    {
        Converters =
        {
            new DecimalStampConverter(),
            new DateStampConverter()
        }
    };

    public static string Stamp(object value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, StampOptions))));

    private sealed class DecimalStampConverter : JsonConverter<decimal>
    {
        public override decimal Read(
            ref Utf8JsonReader reader,
            Type type,
            JsonSerializerOptions options) =>
            reader.GetDecimal();

        public override void Write(
            Utf8JsonWriter writer,
            decimal value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }


    private sealed class DateStampConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(
            ref Utf8JsonReader reader,
            Type type,
            JsonSerializerOptions options) =>
            reader.GetDateTime();

        public override void Write(
            Utf8JsonWriter writer,
            DateTime value,
            JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Ticks);
    }


    public static void Require(
        bool condition,
        string message,
        string? field = null,
        string code = "VALIDATION")
    {
        if (!condition)
            throw new FarmRuleException(message, code, field);
    }


    public static void Text(
        string? value,
        int max,
        string field,
        bool required = true) =>
        Require((!required || !string.IsNullOrWhiteSpace(value)) && (value?.Trim().Length ?? 0) <= max, FarmManagementMessages.Field(field), field);

    public static void Amount(
        decimal value,
        string field,
        bool positive = true,
        string? message = null) =>
        Require((positive ? value > 0 : value >= 0) && value <= 999999999999999m && decimal.Round(value, 3) == value, message ?? FarmManagementMessages.Quantity, field);

    public static decimal ConvertUnit(decimal amount, string from, string to)
    {
        if (string.Equals(from.Trim(), to.Trim(), StringComparison.OrdinalIgnoreCase))
            return amount;
        (string group, decimal factor) Measure(string u) => u.Trim().ToLowerInvariant() switch
        {
            "kg" => ("mass", 1),
            "g" => ("mass", .001m),
            "tấn" or "tan" => ("mass", 1000),
            "tạ" => ("mass", 100),
            "yến" => ("mass", 10),
            "l" or "lít" => ("volume", 1),
            "ml" => ("volume", .001m),
            _ => ("", 0)};
        var a = Measure(from);
        var b = Measure(to);
        Require(a.group != "" && a.group == b.group, FarmManagementMessages.Unit, "unit");
        return amount * a.factor / b.factor;
    }


    public static void Query(FarmQuery q)
    {
        Require(q.Page is >= 1 and <= 100000 && q.PageSize is >= 1 and <= 100 && (q.Keyword?.Length ?? 0) <= 255 && (q.Status?.Length ?? 0) <= 50 && (q.Activity?.Length ?? 0) <= 100 && (!q.From.HasValue || !q.To.HasValue || q.From <= q.To) && (!q.ProductId.HasValue || q.ProductId > 0) && (!q.SeasonId.HasValue || q.SeasonId > 0), FarmManagementMessages.MSG120);
    }


    public static void Proof(
        IFarmerEvidenceStorage storage,
        long actor,
        string[]? refs,
        bool required,
        string message)
    {
        Require(refs is not null && (!required || refs.Length > 0) && refs.Length <= 20 && refs.Distinct(StringComparer.Ordinal).Count() == refs.Length && refs.All(x => x is not null && storage.Owns(actor, x)), message, "evidenceUrls");
    }


    public static IReadOnlyList<FarmHistory> History(IReadOnlyList<AuditLog> logs) =>
        logs.Select(x => new FarmHistory(x.AuditLogId, x.Action, x.Reason, x.OccurredAt, x.PerformedByUser?.FullName, x.OldValue, x.NewValue)).ToList();
}

public abstract class FarmerManagementService(IFarmerManagementRepository repository, IUnitOfWork unit)
{
    protected IUnitOfWork Unit => unit;


    protected async Task<FeatureResult<T>> Read<T>(long actor, Func<Task<T>> body, CancellationToken ct)
    {
        try
        {
            await Access(actor, false, ct);
            return FeatureResult<T>.Ok(await body());
        }
        catch (FarmRuleException e)
        {
            return Failure<T>(e);
        }
    }


    protected async Task<FarmerProfile> Access(long actor, bool update, CancellationToken ct)
    {
        var f = await repository.GetFarmerAsync(actor, update, ct);
        FarmState.Require(f is not null && f.User.Status == "Active" && f.User.Role == "Farmer" && f.VerificationStatus == "Approved", FarmManagementMessages.MSG121, code: "FORBIDDEN");
        return f!;
    }


    protected async Task<FeatureResult<T>> Write<T>(
        long actor,
        FarmCommand cmd,
        bool existing,
        Func<FarmerProfile, Task<T>> body,
        CancellationToken ct)
    {
        try
        {
            FarmState.Require(Guid.TryParseExact(cmd.RequestKey, "D", out _) && (!existing || cmd.Version?.Length == 64), FarmManagementMessages.MSG120);
            await unit.BeginAsync(ct);
            var farm = await Access(actor, true, ct);
            FarmState.Require(!await repository.HasRequestAsync(cmd.RequestKey, ct), FarmManagementMessages.MSG122, code: "CONFLICT");
            var result = await body(farm);
            await unit.CommitAsync(ct);
            return FeatureResult<T>.Ok(result);
        }
        catch (FarmRuleException e)
        {
            await unit.RollbackAsync(CancellationToken.None);
            return Failure<T>(e);
        }
        catch
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw;
        }
    }


    protected static void Match(
        string? expected,
        string actual) =>
        FarmState.Require(expected == actual, FarmManagementMessages.MSG122, code: "CONFLICT");

    protected static T Owned<T>(T? value)
        where T : class
    {
        FarmState.Require(value is not null, FarmManagementMessages.MSG121, code: "FORBIDDEN");
        return value!;
    }


    private static FeatureResult<T> Failure<T>(FarmRuleException e) =>
        new(default, new FeatureError(e.Code, e.Message, e.Field is null ? null : new Dictionary<string, string[]> { { e.Field, [e.Message] } }));
}
