namespace DokPortal.Application.Common;

/// <summary>Adres portalu (frontendu) do linków w e-mailach; pusty = linki względne.</summary>
public sealed record PortalUrl(string BaseUrl)
{
    public string Link(string pathAndQuery) => BaseUrl.TrimEnd('/') + "/" + pathAndQuery.TrimStart('/');
}
