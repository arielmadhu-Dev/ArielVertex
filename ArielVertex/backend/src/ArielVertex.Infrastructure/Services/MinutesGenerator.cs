using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArielVertex.Application.Abstractions;
using ArielVertex.Infrastructure.Integration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Services;

/// <summary>
/// Produces structured, grammar-corrected minutes from raw meeting notes. The built-in generator runs
/// offline (structuring + deterministic grammar/formatting cleanup). When AI is configured and the
/// <c>minutes.aiPolish</c> setting is on, it asks the model (default Claude) for polished minutes and
/// falls back to the built-in output on any error — so a preview is always produced.
/// </summary>
public class MinutesGenerator : IMinutesGenerator
{
    private readonly AiSettings _ai;
    private readonly IPlatformConfig _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<MinutesGenerator> _log;
    private static readonly JsonSerializerOptions J = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNameCaseInsensitive = true };

    public MinutesGenerator(IOptions<AiSettings> ai, IPlatformConfig config, IHttpClientFactory httpFactory, ILogger<MinutesGenerator> log)
    { _ai = ai.Value; _config = config; _httpFactory = httpFactory; _log = log; }

    public async Task<MinutesDraft> GenerateAsync(string title, DateTime meetingDate, string? location,
        IReadOnlyList<string> attendees, string rawNotes, CancellationToken ct = default)
    {
        var builtIn = BuildStructured(title, meetingDate, location, attendees, rawNotes);

        var aiPolish = await _config.GetBoolAsync("minutes.aiPolish", true, ct);
        if (aiPolish && _ai.IsConfigured)
        {
            try
            {
                var polished = await CallModelAsync(title, meetingDate, location, attendees, rawNotes, ct);
                if (!string.IsNullOrWhiteSpace(polished)) return new MinutesDraft(polished.Trim(), true);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "AI minutes generation failed; falling back to the built-in generator.");
            }
        }
        return new MinutesDraft(builtIn, false);
    }

    // ---------- Built-in structuring + grammar cleanup (offline default) ----------
    private static string BuildStructured(string title, DateTime meetingDate, string? location,
        IReadOnlyList<string> attendees, string rawNotes)
    {
        var decisions = new List<string>();
        var actions = new List<string>();
        var discussion = new List<string>();

        foreach (var raw in rawNotes.Split('\n'))
        {
            var line = raw.Trim().TrimStart('-', '*', '•', '·', '\t', ' ');
            if (line.Length == 0) continue;
            var lower = line.ToLowerInvariant();
            var clean = Polish(line);
            if (Regex.IsMatch(lower, @"\b(decid|agreed|approv|resolv|conclud|finaliz|sign(ed)? off)\b"))
                decisions.Add(clean);
            else if (Regex.IsMatch(lower, @"\b(action|will |to-?do|assign|follow[- ]?up|responsible|owner|due|by (mon|tue|wed|thu|fri|sat|sun|next|eod|tomorrow)|@)\b"))
                actions.Add(clean);
            else
                discussion.Add(clean);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Minutes of Meeting — {Polish(title, forceStop: false)}");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {meetingDate:dddd, dd MMM yyyy, HH:mm}");
        if (!string.IsNullOrWhiteSpace(location)) sb.AppendLine($"**Location:** {location.Trim()}");
        sb.AppendLine($"**Attendees:** {(attendees.Count > 0 ? string.Join(", ", attendees) : "—")}");
        sb.AppendLine();

        sb.AppendLine("## Discussion");
        if (discussion.Count > 0) foreach (var d in discussion) sb.AppendLine($"- {d}");
        else sb.AppendLine("- No discussion points were recorded.");
        sb.AppendLine();

        sb.AppendLine("## Decisions");
        if (decisions.Count > 0) foreach (var d in decisions) sb.AppendLine($"- {d}");
        else sb.AppendLine("- No decisions were recorded.");
        sb.AppendLine();

        sb.AppendLine("## Action Items");
        if (actions.Count > 0) foreach (var a in actions) sb.AppendLine($"- {a}");
        else sb.AppendLine("- No action items were recorded.");

        return sb.ToString().TrimEnd();
    }

    /// <summary>Deterministic grammar/formatting cleanup — the "grammar correction" applied automatically.</summary>
    private static string Polish(string text, bool forceStop = true)
    {
        var s = text.Trim();
        if (s.Length == 0) return s;
        s = Regex.Replace(s, @"\s+", " ");                 // collapse runs of whitespace
        s = Regex.Replace(s, @"\s+([,.;:!?])", "$1");        // no space before punctuation
        s = Regex.Replace(s, @"([,;:])(?=\S)", "$1 ");        // one space after a comma/semicolon
        s = Regex.Replace(s, @"\bi\b", "I");                   // standalone "i" -> "I"
        s = char.ToUpper(s[0]) + s.Substring(1);               // capitalise first letter
        if (forceStop && !Regex.IsMatch(s, @"[.!?:]$")) s += ".";
        return s;
    }

    // ---------- Config-gated AI (Claude via HTTP, mirroring the Graph REST pattern) ----------
    private async Task<string?> CallModelAsync(string title, DateTime meetingDate, string? location,
        IReadOnlyList<string> attendees, string rawNotes, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("anthropic");
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("x-api-key", _ai.ApiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var system =
            "You are an assistant that writes clear, professional minutes of a meeting from rough notes. " +
            "Correct all grammar, spelling and punctuation. Organise the content under Markdown headings: " +
            "Discussion, Decisions, and Action Items (attribute an owner to each action item when the notes imply one). " +
            "Start with an H1 title line and a metadata block (Date, Location, Attendees). Do not invent facts that " +
            "are not supported by the notes. Return only the minutes in Markdown.";

        var userText = new StringBuilder()
            .AppendLine($"Title: {title}")
            .AppendLine($"Date: {meetingDate:dddd, dd MMM yyyy, HH:mm}")
            .AppendLine($"Location: {location}")
            .AppendLine($"Attendees: {string.Join(", ", attendees)}")
            .AppendLine()
            .AppendLine("Raw notes:")
            .AppendLine(rawNotes)
            .ToString();

        var payload = new
        {
            model = _ai.Model,
            max_tokens = _ai.MaxTokens,
            system,
            messages = new[] { new { role = "user", content = userText } }
        };

        using var resp = await http.PostAsJsonAsync($"{_ai.BaseUrl.TrimEnd('/')}/messages", payload, J, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(J, ct);
        return body?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
    }

    private class AnthropicResponse { public List<AnthropicBlock>? Content { get; set; } }
    private class AnthropicBlock { public string? Type { get; set; } public string? Text { get; set; } }
}
