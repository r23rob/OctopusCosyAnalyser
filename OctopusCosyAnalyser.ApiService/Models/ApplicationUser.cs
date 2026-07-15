namespace OctopusCosyAnalyser.ApiService.Models;

public class ApplicationUser
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
