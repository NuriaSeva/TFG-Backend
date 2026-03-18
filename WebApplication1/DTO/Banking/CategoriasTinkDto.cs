using System.Text.Json.Serialization;

namespace FindMind.DTOs.Banking;

public class TinkCategoriaDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("defaultChild")]
    public bool DefaultChild { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("primaryName")]
    public string PrimaryName { get; set; } = string.Empty;

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("secondaryName")]
    public string? SecondaryName { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = string.Empty;
}