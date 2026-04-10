namespace OctopusCosyAnalyser.ApiService.Models;

public class TariffRate
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public decimal UnitRatePence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
