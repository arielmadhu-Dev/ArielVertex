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
/// AI-powered bill/invoice data extraction using Claude's vision capabilities. Sends the uploaded
/// file (PDF or image) to the Anthropic Messages API and returns structured bill fields.
/// Falls back gracefully — never throws to the caller.
/// </summary>
public class BillExtractor : IBillExtractor
{
    private readonly AiSettings _ai;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<BillExtractor> _log;
    private static readonly JsonSerializerOptions J = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public BillExtractor(IOptions<AiSettings> ai, IHttpClientFactory httpFactory, ILogger<BillExtractor> log)
    { _ai = ai.Value; _httpFactory = httpFactory; _log = log; }

    public bool IsConfigured => _ai.IsConfigured;

    public async Task<Application.Contracts.ExtractedBillData?> ExtractAsync(
        byte[] fileContent, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!_ai.IsConfigured)
        {
            _log.LogDebug("AI bill extraction skipped — AI service not configured.");
            return null;
        }

        try
        {
            var base64 = Convert.ToBase64String(fileContent);
            var mediaType = contentType.ToLowerInvariant() switch
            {
                "application/pdf" => "application/pdf",
                "image/png" => "image/png",
                "image/jpeg" or "image/jpg" => "image/jpeg",
                "image/gif" => "image/gif",
                _ => "application/pdf"
            };

            var system =
                "You are a bill/invoice data extraction assistant. " +
                "Extract the following fields from the uploaded bill document and return ONLY a JSON object with these fields:\n" +
                "- vendor (string): the company or person who issued the bill\n" +
                "- amount (number): the total amount due (numeric only, no currency symbol)\n" +
                "- dueDate (string|null): the payment due date in YYYY-MM-DD format, or null if not found\n" +
                "- billDate (string|null): the bill/invoice date in YYYY-MM-DD format, or null if not found\n" +
                "- category (string): suggest a category for this bill (e.g. 'Office Rent', 'Software Subscription', 'Utilities', 'Marketing', etc.)\n" +
                "- invoiceNumber (string|null): the invoice/bill number, or null if not found\n\n" +
                "Return ONLY valid JSON. No markdown, no explanation, no code fences.";

            var imageBlock = new
            {
                type = "image",
                source = new
                {
                    type = "base64",
                    media_type = mediaType,
                    data = base64
                }
            };

            var textBlock = new
            {
                type = "text",
                text = "Extract the bill data from this document."
            };

            var payload = new
            {
                model = _ai.Model,
                max_tokens = _ai.MaxTokens,
                system,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[] { imageBlock, textBlock }
                    }
                }
            };

            var http = _httpFactory.CreateClient("anthropic");
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("x-api-key", _ai.ApiKey);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await http.PostAsJsonAsync($"{_ai.BaseUrl.TrimEnd('/')}/messages", payload, J, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(J, ct);
            var text = body?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                _log.LogWarning("AI returned empty response for bill extraction.");
                return null;
            }

            // Strip markdown code fences if present
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                text = Regex.Replace(text, @"^```(?:json)?\s*\n?", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                text = Regex.Replace(text, @"\n?```\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                text = text.Trim();
            }

            var result = JsonSerializer.Deserialize<ExtractedBillJson>(text, J);
            if (result is null)
            {
                _log.LogWarning("Failed to deserialize AI extraction response.");
                return null;
            }

            return new Application.Contracts.ExtractedBillData(
                Vendor: result.Vendor,
                Amount: result.Amount,
                DueDate: result.DueDate,
                BillDate: result.BillDate,
                Category: result.Category,
                InvoiceNumber: result.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AI bill extraction failed for file '{FileName}'.", fileName);
            return null;
        }
    }

    private class AnthropicResponse { public List<AnthropicBlock>? Content { get; set; } }
    private class AnthropicBlock { public string? Type { get; set; } public string? Text { get; set; } }

    private class ExtractedBillJson
    {
        public string? Vendor { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? BillDate { get; set; }
        public string? Category { get; set; }
        public string? InvoiceNumber { get; set; }
    }
}
