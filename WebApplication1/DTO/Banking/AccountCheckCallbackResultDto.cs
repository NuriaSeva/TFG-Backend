namespace FindMind.DTO.Banking;

public class AccountCheckCallbackResultDto
{
    public string LocalUserId { get; set; } = string.Empty;
    public string? State { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
    public string? AccountVerificationReportId { get; set; }

    public Dictionary<string, string> Received { get; set; } = new();
}