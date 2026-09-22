namespace DokPortal.Domain.Constants;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Biskup = "Biskup";
    public const string DyrektorSKSP = "DyrektorSKSP";
    public const string DyrektorDOK = "DyrektorDOK";
    public const string Superwizor = "Superwizor";
    public const string KatechistaProwadzacy = "KatechistaProwadzacy";

    public static readonly string[] All =
    {
        Administrator, Biskup, DyrektorSKSP, DyrektorDOK, Superwizor, KatechistaProwadzacy
    };
}
