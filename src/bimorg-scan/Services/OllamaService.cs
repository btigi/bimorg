using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using OllamaSharp;

namespace Bimorg.Scan.Services;

public sealed class OllamaService(IConfiguration configuration)
{
    private static readonly Regex JsonFence =
        new("```(?:json)?\\s*(.*?)\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<DescribeImageResult> DescribeImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var url = configuration["OllamaUrl"] ?? throw new InvalidOperationException("OllamaUrl not configured.");
        var model = configuration["Model"] ?? throw new InvalidOperationException("Model not configured.");
        var prompt = configuration["Prompt"] ?? throw new InvalidOperationException("Prompt not configured.");

        var ollama = new OllamaApiClient(url, model);
        var chat = new Chat(ollama);

        var sb = new StringBuilder();
        await foreach (var token in chat.SendAsync(prompt, new[] { imageBytes }).WithCancellation(cancellationToken).ConfigureAwait(false))
            sb.Append(token);

        var raw = sb.ToString().Trim();
        return ParseStructuredResponse(raw);
    }

    private static DescribeImageResult ParseStructuredResponse(string raw)
    {
        var jsonSlice = ExtractJsonObject(raw);

        DescribeDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<DescribeDto>(
                jsonSlice,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Vision model returned invalid JSON. Raw response:\n{raw}", ex);
        }

        if (dto?.Description is null || dto.Keywords is null || dto.Keywords.Length == 0)
            throw new InvalidOperationException($"Vision model JSON missing fields. Raw response:\n{raw}");

        var keywords = dto.Keywords
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeWord)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keywords.Length == 0)
            throw new InvalidOperationException($"Vision model produced no usable keywords. Raw response:\n{raw}");

        return new DescribeImageResult(dto.Description.Trim(), keywords);
    }

    /// <summary>Pulls substring from first { to matching } handling common ```json fences.</summary>
    private static string ExtractJsonObject(string raw)
    {
        var text = raw;
        var m = JsonFence.Match(text);
        if (m.Success)
            text = m.Groups[1].Value.Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return text;
        return text[start..(end + 1)];
    }

    private static string NormalizeWord(string s)
        => Regex.Replace(s.Trim().ToLowerInvariant(), "\\s+", " ");

    private sealed class DescribeDto
    {
        public string? Description { get; init; }
        public string[]? Keywords { get; init; }
    }
}

public readonly record struct DescribeImageResult(string Description, IReadOnlyList<string> Keywords);
