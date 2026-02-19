namespace OctopusCosyAnalyser.ApiService.Models;

public class OctopusAccountSettings
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

