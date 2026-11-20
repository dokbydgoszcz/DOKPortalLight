using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DokPortal.Api.Security;

/// <summary>Link z zaproszenia ważny dłużej niż zwykły reset hasła (2 h): nowa osoba nie zawsze czyta pocztę od razu.</summary>
public class InvitationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public InvitationTokenProviderOptions()
    {
        Name = InvitationTokens.ProviderName;
        TokenLifespan = TimeSpan.FromDays(7);
    }
}

/// <summary>Dostawca kodów zaproszenia (<see cref="InvitationTokens"/>).</summary>
public class InvitationTokenProvider : DataProtectorTokenProvider<AppUser>
{
    public InvitationTokenProvider(IDataProtectionProvider dataProtectionProvider, IOptions<InvitationTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<AppUser>> logger) : base(dataProtectionProvider, options, logger)
    {
    }
}
