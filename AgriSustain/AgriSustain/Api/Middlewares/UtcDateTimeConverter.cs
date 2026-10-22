using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgriSustain.Api.Middlewares;

public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type type,
        JsonSerializerOptions options) => DateTime.Parse(reader.GetString() ?? "",
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal
                | DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTime value,
        JsonSerializerOptions options) => writer.WriteStringValue(
            DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

