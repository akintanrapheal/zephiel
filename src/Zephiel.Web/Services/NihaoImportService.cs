using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Zephiel.Web.Data;
using Zephiel.Web.Models.Domain;

namespace Zephiel.Web.Services;

public class NihaoImportResult
{
    public string SourceUrl { get; set; } = "";
    public bool Success { get; set; }
    public bool Created { get; set; }
    public string? Error { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string CategorySlug { get; set; } = "";
    public int ImageCount { get; set; }
    public decimal Cost { get; set; }
    public decimal Retail { get; set; }
}

/// <summary>
/// Imports a product from a Nihaojewelry product URL (the merchant's authorised supplier).
/// Reads the product data embedded in the page (window.__INITIAL__ state), downloads and
/// re-hosts the images locally, maps the supplier category to one of the store categories, and
/// creates/updates the product as a draft (inactive, stock 0) keyed on ExternalCode = NIHAO-{code}.
/// Wholesale USD becomes the product cost (converted at a configurable FX rate) and a suggested
/// retail price is set at cost × a configurable markup, so nothing goes live at wholesale by mistake.
/// </summary>
public class NihaoImportService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ISettingsService _settings;
    private readonly SeoDescriptionGenerator _seo;
    private readonly ILogger<NihaoImportService> _logger;

    private const string ImgBase = "https://img.nihaojewelry.com/";
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36";

    public NihaoImportService(ApplicationDbContext db, IHttpClientFactory httpFactory, IWebHostEnvironment env,
        ISettingsService settings, SeoDescriptionGenerator seo, ILogger<NihaoImportService> logger)
    {
        _db = db; _httpFactory = httpFactory; _env = env; _settings = settings; _seo = seo; _logger = logger;
    }

    public async Task<NihaoImportResult> ImportAsync(string url)
    {
        var res = new NihaoImportResult { SourceUrl = url };
        try
        {
            url = (url ?? "").Trim();
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || !url.Contains("nihaojewelry"))
            {
                res.Error = "Not a Nihaojewelry URL.";
                return res;
            }

            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(40);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(Ua);
            http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            var pageBytes = await FetchAsync(url, http);
            if (pageBytes == null || pageBytes.Length == 0)
            {
                res.Error = "Could not fetch the product page (network or anti-bot block).";
                return res;
            }
            var html = Encoding.UTF8.GetString(pageBytes);
            var data = ExtractProduct(html, url);
            if (data == null || string.IsNullOrWhiteSpace(data.Title))
            {
                res.Error = "Could not read product data from the page (layout may have changed).";
                return res;
            }

            // Category mapping
            var catSlug = MapCategory(data);
            var category = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == catSlug)
                        ?? await _db.Categories.OrderBy(c => c.SortOrder).FirstOrDefaultAsync();
            if (category == null) { res.Error = "No categories exist to import into."; return res; }

            // Pricing: wholesale USD → cost (₦) → suggested retail
            var fx = await _settings.GetDecimalAsync("import.nihao_fx_ngn_per_usd", 1600m);
            var markup = await _settings.GetDecimalAsync("import.nihao_markup", 3.0m);
            var costNgn = Math.Round(data.MinPriceUsd * fx, 0);
            var retailNgn = Math.Round(costNgn * markup, 0);

            var code = data.ExternalCode;
            var existing = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.ExternalCode == code);
            var product = existing ?? new Product { ExternalCode = code, IsActive = false, TrackStock = true, ProductType = "simple" };

            product.Name = data.Title;
            if (existing == null) product.Slug = await UniqueSlugAsync(Slugify(data.Title));
            product.CategoryId = category.Id;
            product.Currency = "NGN";
            product.CostPrice = costNgn;
            product.Price = retailNgn;
            product.Sku ??= code;
            product.Material = data.Attributes.GetValueOrDefault("Material");
            product.GemstoneType = data.Attributes.GetValueOrDefault("Inlay Material");
            product.Weight = data.Attributes.GetValueOrDefault("Weight");
            product.IsActive = false; // stays a draft until reviewed

            var seed = int.TryParse(Regex.Match(code, @"\d+").Value, out var v) ? v : Math.Abs(code.GetHashCode());
            var marketing = _seo.Build(seed, data.Title, category.Name);
            product.Description = ProductHtml.Sanitize(marketing + SpecsHtml(data.Attributes));
            product.ShortDescription = _seo.BuildShort(seed, data.Title, category.Name);

            // Images — download & re-host once (skip if the product already has images on re-import).
            if (product.Images.Count == 0 && data.Images.Count > 0)
            {
                var dir = Path.Combine(_env.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(dir);
                var sort = 0;
                foreach (var imgUrl in data.Images)
                {
                    try
                    {
                        var bytes = await FetchAsync(imgUrl, http);
                        if (bytes == null || bytes.Length < 512) continue; // skip error/placeholder responses
                        var ext = imgUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? ".png"
                                : imgUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".jpg";
                        var fname = Guid.NewGuid().ToString("N") + ext;
                        await File.WriteAllBytesAsync(Path.Combine(dir, fname), bytes);
                        sort++;
                        product.Images.Add(new ProductImage
                        {
                            Url = "/uploads/products/" + fname,
                            IsPrimary = sort == 1,
                            SortOrder = sort,
                            AltText = data.Title
                        });
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Nihao image download failed: {Url}", imgUrl); }
                }
            }

            if (existing == null) _db.Products.Add(product);
            await _db.SaveChangesAsync();

            res.Success = true;
            res.Created = existing == null;
            res.ProductId = product.Id;
            res.Name = product.Name;
            res.Slug = product.Slug;
            res.CategorySlug = category.Slug;
            res.ImageCount = product.Images.Count;
            res.Cost = costNgn;
            res.Retail = retailNgn;
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nihao import failed for {Url}", url);
            res.Error = ex.Message;
            return res;
        }
    }

    // Fetch bytes for a URL. Nihao's anti-bot blocks .NET's TLS fingerprint, so try curl first
    // (present on Windows 10+ and virtually all Linux) and fall back to HttpClient for other hosts.
    private async Task<byte[]?> FetchAsync(string url, HttpClient http)
    {
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "nh_" + Guid.NewGuid().ToString("N"));
            var psi = new ProcessStartInfo
            {
                FileName = "curl",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in new[] { "-s", "-L", "--compressed", "--max-time", "40", "-A", Ua, "-o", tmp, url })
                psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                if (proc.ExitCode == 0 && File.Exists(tmp))
                {
                    var bytes = await File.ReadAllBytesAsync(tmp);
                    try { File.Delete(tmp); } catch { /* best-effort */ }
                    if (bytes.Length > 0) return bytes;
                }
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "curl fetch unavailable, falling back to HttpClient: {Url}", url); }

        try { return await http.GetByteArrayAsync(url); }
        catch (Exception ex) { _logger.LogWarning(ex, "Fetch failed: {Url}", url); return null; }
    }

    // ─── Extraction ────────────────────────────────────────────────────────────
    private sealed class Extracted
    {
        public string ExternalCode = "";
        public string Title = "";
        public decimal MinPriceUsd;
        public List<string> Images = new();
        public Dictionary<string, string> Attributes = new();
        public List<string> Crumbs = new();
    }

    private static Extracted? ExtractProduct(string html, string url)
    {
        var m = Regex.Match(html, @"window\.__INITIAL[_A-Z]*\s*=\s*");
        if (!m.Success) return null;
        var startBrace = html.IndexOf('{', m.Index + m.Length);
        if (startBrace < 0) return null;
        var json = ExtractBraced(html, startBrace);
        if (json == null) return null;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("productDetails", out var pdEl)) return null;
        if (!pdEl.TryGetProperty("info", out var info)) return null;

        var e = new Extracted();

        // code — from the URL (…-nh10328521.html), fallback to spu/productGroupID
        var codeM = Regex.Match(url, @"-(nh\d+)\.html", RegexOptions.IgnoreCase);
        var rawCode = codeM.Success ? codeM.Groups[1].Value : (Str(info, "spu") ?? "");
        e.ExternalCode = "NIHAO-" + rawCode.ToUpperInvariant();

        e.Title = CleanTitle(Str(info, "name") ?? "");

        if (info.TryGetProperty("minPrice", out var mp) && mp.ValueKind == JsonValueKind.Number)
            e.MinPriceUsd = mp.GetDecimal();

        if (info.TryGetProperty("imgList", out var il) && il.ValueKind == JsonValueKind.Array)
            foreach (var x in il.EnumerateArray())
                if (x.GetString() is string p && p.Length > 0) e.Images.Add(ImgBase + p.TrimStart('/'));

        if (info.TryGetProperty("crumbs", out var cr) && cr.ValueKind == JsonValueKind.Array)
            foreach (var x in cr.EnumerateArray())
                if (x.TryGetProperty("name", out var nm) && nm.GetString() is string s) e.Crumbs.Add(s);

        if (info.TryGetProperty("detailJson", out var dj) && dj.GetString() is string djs && djs.Length > 1)
        {
            try
            {
                using var da = JsonDocument.Parse(djs);
                foreach (var p in da.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String) e.Attributes[p.Name] = p.Value.GetString() ?? "";
            }
            catch { /* attributes are best-effort */ }
        }
        return e;
    }

    private static string? Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string ExtractBraced(string s, int start)
    {
        int depth = 0; bool instr = false, esc = false; char q = '"';
        for (int j = start; j < s.Length; j++)
        {
            var ch = s[j];
            if (instr)
            {
                if (esc) esc = false;
                else if (ch == '\\') esc = true;
                else if (ch == q) instr = false;
            }
            else
            {
                if (ch == '"' || ch == '\'') { instr = true; q = ch; }
                else if (ch == '{') depth++;
                else if (ch == '}') { depth--; if (depth == 0) return s.Substring(start, j - start + 1); }
            }
        }
        return null!;
    }

    private string MapCategory(Extracted e)
    {
        var cls = e.Attributes.GetValueOrDefault("Classification", "");
        var hay = (cls + " " + string.Join(" ", e.Crumbs) + " " + e.Title).ToLowerInvariant();
        // A multi-piece classification (e.g. "Rings, Earrings, Necklace") or a "set" is a Set.
        if (cls.Contains(',') || hay.Contains("set")) return "sets";
        if (hay.Contains("clutch") || hay.Contains("purse") || hay.Contains("handbag") || hay.Contains(" bag")) return "clutches";
        if (hay.Contains("earring")) return "earrings";
        if (hay.Contains("necklace") || hay.Contains("pendant")) return "necklaces";
        if (hay.Contains("bracelet") || hay.Contains("bangle")) return "bracelets";
        if (hay.Contains("ring")) return "rings";
        return "sets";
    }

    private static string SpecsHtml(Dictionary<string, string> attrs)
    {
        if (attrs.Count == 0) return "";
        var sb = new StringBuilder("<h3>Specifications</h3><ul>");
        foreach (var kv in attrs)
        {
            if (string.IsNullOrWhiteSpace(kv.Value)) continue;
            sb.Append($"<li><strong>{System.Net.WebUtility.HtmlEncode(kv.Key)}:</strong> {System.Net.WebUtility.HtmlEncode(kv.Value)}</li>");
        }
        sb.Append("</ul>");
        return sb.ToString();
    }

    // Nihao titles are long/keyword-stuffed; keep them but trim to a sensible length on a word boundary.
    private static string CleanTitle(string t)
    {
        t = Regex.Replace((t ?? "").Trim(), @"\s+", " ");
        if (t.Length <= 90) return t;
        var cut = t[..90];
        var sp = cut.LastIndexOf(' ');
        return (sp > 40 ? cut[..sp] : cut).TrimEnd(',', ' ');
    }

    private async Task<string> UniqueSlugAsync(string baseSlug)
    {
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "product";
        var slug = baseSlug; var n = 1;
        while (await _db.Products.AnyAsync(p => p.Slug == slug)) slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private static string Slugify(string name) =>
        Regex.Replace((name ?? "").ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}
