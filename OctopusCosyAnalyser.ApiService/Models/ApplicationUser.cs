using Microsoft.AspNetCore.Identity;

namespace OctopusCosyAnalyser.ApiService.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
