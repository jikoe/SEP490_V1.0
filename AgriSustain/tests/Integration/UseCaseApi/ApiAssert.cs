using System.Net;
using System.Text.Json;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

internal static class ApiAssert
{
    /// <summary>Asserts the status and returns the response body as JSON (empty object when there is none).</summary>
    public static async Task<JsonElement> Status(HttpResponseMessage response, HttpStatusCode expected)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {body}");
        if (body.Length == 0)
        {
            return JsonDocument.Parse("{}").RootElement;
        }

        // A string result is written as text/plain; expose it as a JSON string.
        return response.Content.Headers.ContentType?.MediaType == "text/plain"
            ? JsonSerializer.SerializeToElement(body)
            : JsonDocument.Parse(body).RootElement;
    }

    public static async Task<JsonElement> Problem(HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        var json = await Status(response, expected);
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    public static void HasFieldError(JsonElement problem, string field) =>
        Assert.Contains(problem.GetProperty("errors").EnumerateObject(),
            property => string.Equals(property.Name, field, StringComparison.OrdinalIgnoreCase));
}
