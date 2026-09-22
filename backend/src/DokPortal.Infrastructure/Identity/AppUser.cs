using Microsoft.AspNetCore.Identity;

namespace DokPortal.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public Guid? PersonId { get; set; }
}
