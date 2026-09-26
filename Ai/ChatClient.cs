using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DiskLens.Ai;

public sealed record ChatMessage(string Role, string Content);

/// <summary>Streams replies from an OpenAI-compatible <c>/chat/completions</c> endpoint.</summary>
public sealed class ChatClient(AiConfig config)
{
    private static readonly HttpClient Http = CreateHttp();

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var body = new
        {
            model = config.Model,
            stream = true,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new ChatException($"AI endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(error, 400)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // Server-sent events: each "data:" line holds one JSON chunk; "[DONE]" ends the stream.
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line[5..].Trim();
            if (data == "[DONE]")
                yield break;

            if (ContentOf(data) is { Length: > 0 } text)
                yield return text;
        }
    }

    private static string? ContentOf(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
            throw new ChatException($"AI endpoint error: {Truncate(error.ToString(), 400)}");

        return root.TryGetProperty("choices", out var choices)
               && choices.ValueKind == JsonValueKind.Array
               && choices.GetArrayLength() > 0
               && choices[0].TryGetProperty("delta", out var delta)
               && delta.TryGetProperty("content", out var content)
               && content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : null;
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        // The gateway sits behind Cloudflare, which rejects requests without a User-Agent.
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DiskLens", "1.0"));
        return http;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}

public sealed class ChatException(string message) : Exception(message);
