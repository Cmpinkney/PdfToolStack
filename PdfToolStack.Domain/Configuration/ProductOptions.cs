namespace PdfToolStack.Domain.Configuration;

public sealed class ProductOptions
{
    public const string SectionName = "Product";

    public string SiteName { get; set; } = "PdfToolStack";

    public string SiteUrl { get; set; } = "https://pdftoolstack.com";

    public string SupportEmail { get; set; } = "admin@pdftoolstack.com";

    public string AdminEmail { get; set; } = "admin@pdftoolstack.com";

    public string SecurityEmail { get; set; } = "security@pdftoolstack.com";

    public string DefaultSeoType { get; set; } = "website";

    public string DefaultTwitterCard { get; set; } = "summary";

    public string[] AllowedReturnHosts { get; set; } = new[]
    {
        "localhost",
        "pdftoolstack.com",
        "www.pdftoolstack.com"
    };
}

public interface IProductContext
{
    string SiteName { get; }

    string SiteUrl { get; }

    string SupportEmail { get; }

    string AdminEmail { get; }

    string SecurityEmail { get; }

    string DefaultSeoType { get; }

    string DefaultTwitterCard { get; }

    IReadOnlyCollection<string> AllowedReturnHosts { get; }

    IReadOnlyCollection<string> SiteOrigins { get; }

    string ResolveUrl(string path);

    bool IsAllowedReturnUrl(string url);
}

public sealed class ProductContext : IProductContext
{
    private readonly ProductOptions _options;
    private readonly string[] _allowedReturnHosts;
    private readonly string[] _siteOrigins;

    public ProductContext(ProductOptions? options)
    {
        _options = options ?? new ProductOptions();
        SiteUrl = NormalizeSiteUrl(_options.SiteUrl);
        _allowedReturnHosts = NormalizeHosts(_options.AllowedReturnHosts);
        _siteOrigins = BuildSiteOrigins(SiteUrl);
    }

    public string SiteName => ValueOrDefault(_options.SiteName, "PdfToolStack");

    public string SiteUrl { get; }

    public string SupportEmail => ValueOrDefault(
        _options.SupportEmail,
        "admin@pdftoolstack.com");

    public string AdminEmail => ValueOrDefault(
        _options.AdminEmail,
        "admin@pdftoolstack.com");

    public string SecurityEmail => ValueOrDefault(
        _options.SecurityEmail,
        "security@pdftoolstack.com");

    public string DefaultSeoType => ValueOrDefault(
        _options.DefaultSeoType,
        "website");

    public string DefaultTwitterCard => ValueOrDefault(
        _options.DefaultTwitterCard,
        "summary");

    public IReadOnlyCollection<string> AllowedReturnHosts => _allowedReturnHosts;

    public IReadOnlyCollection<string> SiteOrigins => _siteOrigins;

    public string ResolveUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            return $"{SiteUrl}/";

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return $"{SiteUrl}{(path.StartsWith('/') ? path : $"/{path}")}";
    }

    public bool IsAllowedReturnUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return _allowedReturnHosts.Contains(
            uri.Host,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ValueOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeSiteUrl(string? siteUrl)
    {
        var normalized = ValueOrDefault(siteUrl, "https://pdftoolstack.com")
            .TrimEnd('/');

        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"https://{normalized}";
        }

        return normalized;
    }

    private static string[] NormalizeHosts(IEnumerable<string>? hosts)
    {
        var configuredHosts = hosts?
            .Select(NormalizeHost)
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return configuredHosts is { Length: > 0 }
            ? configuredHosts
            : new[] { "localhost", "pdftoolstack.com", "www.pdftoolstack.com" };
    }

    private static string? NormalizeHost(string? host)
    {
        host = host?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(host))
            return null;

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
            return uri.Host;

        return host;
    }

    private static string[] BuildSiteOrigins(string siteUrl)
    {
        var origins = new List<string>();

        if (Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri))
        {
            origins.Add(uri.GetLeftPart(UriPartial.Authority));

            if (!uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(uri)
                {
                    Host = $"www.{uri.Host}",
                    Path = string.Empty,
                    Query = string.Empty,
                    Fragment = string.Empty
                };

                origins.Add(builder.Uri.GetLeftPart(UriPartial.Authority));
            }
        }

        return origins
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
