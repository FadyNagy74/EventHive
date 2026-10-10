using Microsoft.AspNetCore.Identity;

namespace AuthenticationService.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
