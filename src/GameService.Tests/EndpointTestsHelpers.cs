using System.Text.Json;
using Xunit;

namespace GameService.Tests;

public static class EndpointTestsHelpers
{
    public static async Task<string> ReadFirstErrorMessage(HttpResponseMessage res)
    {
        var content = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors))
            {
                if (errors.ValueKind == JsonValueKind.Object
                    && errors.TryGetProperty("", out var def)
                    && def.ValueKind == JsonValueKind.Array
                    && def.GetArrayLength() > 0)
                {
                    return def[0].GetString() ?? content;
                }

                foreach (var prop in errors.EnumerateObject())
                {
                    var v = prop.Value;
                    if (v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0)
                    {
                        return v[0].GetString() ?? content;
                    }

                    if (v.ValueKind == JsonValueKind.String)
                    {
                        return v.GetString() ?? content;
                    }
                }
            }

            if (root.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? content;
            }

            if (root.TryGetProperty("title", out var title))
            {
                return title.GetString() ?? content;
            }

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.String)
            {
                return root[0].GetString() ?? content;
            }
        }
        catch (JsonException) { }

        return content;
    }
}