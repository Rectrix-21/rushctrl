using Microsoft.AspNetCore.Identity;

namespace RushCtrl.Api.Modules.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
}

public sealed class Membership
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public string Permission { get; set; } = "Employee";
    public int MaxWeeklyHours { get; set; } = 40;
}
