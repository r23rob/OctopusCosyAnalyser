namespace OctopusCosyAnalyser.ApiService.Models;

public class HeatPumpInfo
{
    public string Id { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime? InstallationDate { get; set; }
}

public class PropertyInfo
{
    public int Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
}
