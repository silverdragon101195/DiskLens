using System.IO;
using System.Text.Json;

namespace DiskLens.Ai;

/// <summary>
/// Endpoint settings read from a BYOK JSON file next to the executable
/// (<c>apiKey</c> plus a <c>models</c> array whose first entry supplies <c>id</c> and <c>url</c>).
/// </summary>
public sealed record AiConfig(string Endpoint, string Model, string ApiKey)
{
    public const string FileName = "Gemini_BYOK.json";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    public static AiConfig Load()
    {
        if (!File.Exists(FilePath))
            throw new AiConfigException($"AI config not found. Put {FileName} next to DiskLens.exe ({AppContext.BaseDirectory}).");

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            var root = doc.RootElement;
            var model = root.GetProperty("models")[0];

            return new AiConfig(
                Endpoint: Required(model, "url").TrimEnd('/') + "/chat/completions",
                Model: Required(model, "id"),
                ApiKey: Required(root, "apiKey"));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                       or IndexOutOfRangeException or IOException)
        {
            throw new AiConfigException($"{FileName} is invalid: {ex.Message}");
        }
    }

    private static string Required(JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(value) ? throw new KeyNotFoundException($"'{name}' is empty.") : value;
    }
}

public sealed class AiConfigException(string message) : Exception(message);
