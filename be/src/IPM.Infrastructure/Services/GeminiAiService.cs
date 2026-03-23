using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IPM.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IPM.Infrastructure.Services;

public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly ILogger<GeminiAiService> _logger;
    private const string BaseUrlPrefix = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiAiService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = configuration["Gemini:ApiKey"];
        _model = configuration["Gemini:Model"] ?? "gemini-3.0-pro";
        _baseUrl = $"{BaseUrlPrefix}/{_model}:generateContent";
        _logger = logger;
    }

    public async Task<AiProgressResult?> AnalyzeProgressImageAsync(string base64Image, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Gemini API key not configured");
            return new AiProgressResult
            {
                ProgressPct = 0,
                Status = "AI not configured",
                AnomaliesDetected = new List<string> { "Gemini API key not configured" }
            };
        }

        try
        {
            var prompt = @"Bạn là Kỹ sư trưởng giám sát thi công nội thất. Hãy phân tích bức ảnh hiện trường này. Nhiệm vụ:
1. Ước tính tỷ lệ hoàn thành (từ 0 đến 100) của hạng mục đang thi công.
2. Phát hiện các vi phạm an toàn lao động hoặc vật tư sắp xếp lộn xộn (nếu có).

Trả về dữ liệu TUYỆT ĐỐI theo định dạng JSON sau (không có text khác):
{""progress_pct"": number, ""status"": ""string"", ""anomalies_detected"": [""string""]}";

            var request = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new()
                    {
                        Parts = new List<GeminiPart>
                        {
                            new() { Text = prompt },
                            new()
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = "image/jpeg",
                                    Data = base64Image
                                }
                            }
                        }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    ResponseMimeType = "application/json"
                }
            };

            var response = await CallGeminiApiAsync(request, cancellationToken);

            if (response != null)
            {
                var text = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    var result = JsonSerializer.Deserialize<GeminiProgressResponse>(text, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null)
                    {
                        return new AiProgressResult
                        {
                            ProgressPct = result.ProgressPct,
                            Status = result.Status ?? "Analyzed",
                            AnomaliesDetected = result.AnomaliesDetected ?? new List<string>()
                        };
                    }
                }
            }

            return new AiProgressResult
            {
                ProgressPct = 0,
                Status = "Analysis failed - no valid response",
                AnomaliesDetected = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing progress image with Gemini AI");
            return new AiProgressResult
            {
                ProgressPct = 0,
                Status = "Analysis failed",
                AnomaliesDetected = new List<string> { $"Analysis error: {ex.Message}" }
            };
        }
    }

    public async Task<AiInvoiceResult?> ExtractInvoiceDataAsync(string base64Image, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Gemini API key not configured");
            return new AiInvoiceResult
            {
                TotalAmount = 0,
                Vendor = "AI not configured",
                InvoiceDate = DateTime.UtcNow
            };
        }

        try
        {
            var prompt = @"Bạn là chuyên gia OCR hóa đơn tài chính. Hãy trích xuất thông tin từ ảnh hóa đơn này.

Trả về dữ liệu TUYỆT ĐỐI theo định dạng JSON sau (không có text khác):
{""vendor"": ""string"", ""total_amount"": number, ""invoice_date"": ""yyyy-MM-dd""}

Nếu không đọc được thông tin nào, để giá trị mặc định: vendor=""Unknown"", total_amount=0, invoice_date=""" + DateTime.UtcNow.ToString("yyyy-MM-dd") + @"""";

            var request = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new()
                    {
                        Parts = new List<GeminiPart>
                        {
                            new() { Text = prompt },
                            new()
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = "image/jpeg",
                                    Data = base64Image
                                }
                            }
                        }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    ResponseMimeType = "application/json"
                }
            };

            var response = await CallGeminiApiAsync(request, cancellationToken);

            if (response != null)
            {
                var text = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    var result = JsonSerializer.Deserialize<GeminiInvoiceResponse>(text, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null)
                    {
                        return new AiInvoiceResult
                        {
                            TotalAmount = result.TotalAmount,
                            Vendor = result.Vendor ?? "Unknown",
                            InvoiceDate = DateTime.TryParse(result.InvoiceDate, out var date) ? date : DateTime.UtcNow
                        };
                    }
                }
            }

            return new AiInvoiceResult
            {
                TotalAmount = 0,
                Vendor = "Unknown",
                InvoiceDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting invoice data with Gemini AI");
            return new AiInvoiceResult
            {
                TotalAmount = 0,
                Vendor = "Error",
                InvoiceDate = DateTime.UtcNow
            };
        }
    }

    private async Task<GeminiResponse?> CallGeminiApiAsync(GeminiRequest request, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}?key={_apiKey}";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<GeminiResponse>(jsonOptions, cancellationToken);
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Gemini API error: {StatusCode} - {Error}", response.StatusCode, error);
        return null;
    }
}

#region Gemini API Models

internal class GeminiRequest
{
    public List<GeminiContent> Contents { get; set; } = new();
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

internal class GeminiContent
{
    public List<GeminiPart> Parts { get; set; } = new();
}

internal class GeminiPart
{
    public string? Text { get; set; }

    [JsonPropertyName("inline_data")]
    public GeminiInlineData? InlineData { get; set; }
}

internal class GeminiInlineData
{
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = "image/jpeg";

    public string Data { get; set; } = "";
}

internal class GeminiGenerationConfig
{
    [JsonPropertyName("response_mime_type")]
    public string ResponseMimeType { get; set; } = "application/json";
}

internal class GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
}

internal class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
}

internal class GeminiProgressResponse
{
    [JsonPropertyName("progress_pct")]
    public int ProgressPct { get; set; }

    public string? Status { get; set; }

    [JsonPropertyName("anomalies_detected")]
    public List<string>? AnomaliesDetected { get; set; }
}

internal class GeminiInvoiceResponse
{
    public string? Vendor { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("invoice_date")]
    public string? InvoiceDate { get; set; }
}

#endregion
