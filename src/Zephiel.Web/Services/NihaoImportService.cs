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

// Overrides supplied from the "Preview & choose" step.
public class NihaoImportOptions
{
    public string? Name { get; set; }
    public decimal? RetailPrice { get; set; }
    public int? CategoryId { get; set; }
    public HashSet<string>? SelectedSkus { get; set; }   // null = import all variants
}

public class NihaoPreviewVariant
{
    public string Sku { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal Retail { get; set; }
    public string? ImageUrl { get; set; }
}

public class NihaoPreview
{
    public string SourceUrl { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Title { get; set; } = "";
    public string CategorySlug { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal Retail { get; set; }
    public List<string> Images { get; set; } = new();
    public List<NihaoPreviewVariant> Variants { get; set; } = new();
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
    private readonly Dictionary<string, ProductAttribute> _attrCache = new();

    private const string ImgBase = "https://img.nihaojewelry.com/";
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36";

    public NihaoImportService(ApplicationDbContext db, IHttpClientFactory httpFactory, IWebHostEnvironment env,
        ISettingsService settings, SeoDescriptionGenerator seo, ILogger<NihaoImportService> logger)
    {
        _db = db; _httpFactory = httpFactory; _env = env; _settings = settings; _seo = seo; _logger = logger;
    }

    public async Task<NihaoImportResult> ImportAsync(string url, NihaoImportOptions? opts = null)
    {
        var res = new NihaoImportResult { SourceUrl = url };
        try
        {
            var (data, err) = await FetchExtractAsync(url);
            if (data == null) { res.Error = err; return res; }

            // Category — explicit override from the preview, else auto-map to the closest store category.
            Category? category = null;
            if (opts?.CategoryId is int cid) category = await _db.Categories.FindAsync(cid);
            category ??= await _db.Categories.FirstOrDefaultAsync(c => c.Slug == MapCategory(data));
            category ??= await _db.Categories.OrderBy(c => c.SortOrder).FirstOrDefaultAsync();
            if (category == null) { res.Error = "No categories exist to import into."; return res; }

            // Pricing: wholesale USD → cost (₦); retail is the edited value if given, else cost × markup.
            var (fx, markup) = await RatesAsync();
            var costNgn = Math.Round(data.MinPriceUsd * fx, 0);
            var retailNgn = (opts?.RetailPrice is decimal rp && rp > 0) ? Math.Round(rp, 0) : Math.Round(costNgn * markup, 0);
            var name = !string.IsNullOrWhiteSpace(opts?.Name) ? opts!.Name!.Trim() : data.Title;

            var code = data.ExternalCode;
            var existing = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.ExternalCode == code);
            var product = existing ?? new Product { ExternalCode = code, IsActive = false, TrackStock = true, ProductType = "simple" };

            product.Name = name;
            if (existing == null) product.Slug = await UniqueSlugAsync(Slugify(name));
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
            product.Description = ProductHtml.Sanitize(_seo.Build(seed, name, category.Name) + SpecsHtml(data.Attributes));
            product.ShortDescription = _seo.BuildShort(seed, name, category.Name);

            // Images — download & re-host once (skip if the product already has images on re-import).
            if (product.Images.Count == 0 && data.Images.Count > 0)
            {
                using var http = NewClient();
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
                            AltText = name
                        });
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Nihao image download failed: {Url}", imgUrl); }
                }
            }

            // Variants — optionally filtered to the SKUs chosen in the preview. Built only for a fresh
            // product so re-imports never touch a variant that might already be referenced by an order.
            var chosen = opts?.SelectedSkus;
            var variantRows = chosen != null ? data.Variants.Where(x => chosen.Contains(x.Sku)).ToList() : data.Variants;
            if (product.Variants.Count == 0 && variantRows.Count > 0)
            {
                foreach (var vr in variantRows)
                {
                    var values = new List<ProductAttributeValue>();
                    if (vr.Color.Length > 0) values.Add(await AttrValueAsync("color", vr.Color));
                    if (vr.Size.Length > 0) values.Add(await AttrValueAsync("size", vr.Size));
                    if (values.Count == 0) continue;
                    // Adjustment is the wholesale price difference from the cheapest variant, so it's
                    // independent of any manual override of the base retail price.
                    var adj = Math.Round((vr.PriceUsd - data.MinPriceUsd) * fx * markup, 0);
                    product.Variants.Add(new ProductVariant
                    {
                        Name = string.Join(" / ", values.Select(x => x.Value)),
                        Sku = string.IsNullOrWhiteSpace(vr.Sku) ? null : vr.Sku,
                        PriceAdjustment = adj > 0 ? adj : null,
                        StockQuantity = 0,
                        IsActive = true,
                        AttributeValues = values
                    });
                }
                if (product.Variants.Count > 0) product.ProductType = "variable";
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

    // Fetch + extract without saving — powers the "Preview & choose" step.
    public async Task<NihaoPreview> PreviewAsync(string url)
    {
        var pv = new NihaoPreview { SourceUrl = (url ?? "").Trim() };
        var (data, err) = await FetchExtractAsync(url);
        if (data == null) { pv.Error = err; return pv; }

        var (fx, markup) = await RatesAsync();
        pv.Success = true;
        pv.Title = data.Title;
        pv.CategorySlug = MapCategory(data);
        pv.Cost = Math.Round(data.MinPriceUsd * fx, 0);
        pv.Retail = Math.Round(pv.Cost * markup, 0);
        pv.Images = data.Images;
        foreach (var vr in data.Variants)
        {
            var label = string.Join(" / ", new[] { vr.Color, vr.Size }.Where(s => !string.IsNullOrWhiteSpace(s)));
            pv.Variants.Add(new NihaoPreviewVariant
            {
                Sku = vr.Sku,
                Label = string.IsNullOrWhiteSpace(label) ? vr.Sku : label,
                Retail = Math.Round(vr.PriceUsd * fx * markup, 0),
                ImageUrl = string.IsNullOrEmpty(vr.Img) ? null : vr.Img,
            });
        }
        return pv;
    }

    private HttpClient NewClient()
    {
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(40);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(Ua);
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return http;
    }

    private async Task<(decimal fx, decimal markup)> RatesAsync() =>
        (await _settings.GetDecimalAsync("import.nihao_fx_ngn_per_usd", 1600m),
         await _settings.GetDecimalAsync("import.nihao_markup", 3.0m));

    private async Task<(Extracted? data, string? error)> FetchExtractAsync(string url)
    {
        url = (url ?? "").Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || !url.Contains("nihaojewelry"))
            return (null, "Not a Nihaojewelry product URL.");
        using var http = NewClient();
        var bytes = await FetchAsync(url, http);
        if (bytes == null || bytes.Length == 0)
            return (null, "Could not fetch the product page (network or anti-bot block).");
        var data = ExtractProduct(Encoding.UTF8.GetString(bytes), url);
        if (data == null || string.IsNullOrWhiteSpace(data.Title))
            return (null, "Could not read product data from the page (layout may have changed).");
        return (data, null);
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
        public List<VariantRow> Variants = new();
    }

    private sealed class VariantRow
    {
        public string Color = "";
        public string Size = "";
        public string Sku = "";
        public decimal PriceUsd;
        public string Img = "";   // Nihao CDN url — used for the preview thumbnail only (not re-hosted)
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

        // Variants / SKUs — Nihao lists each option in itemList (color = design/colour, size = size).
        if (info.TryGetProperty("itemList", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in items.EnumerateArray())
            {
                var vr = new VariantRow
                {
                    Color = (Str(it, "color") ?? "").Trim(),
                    Size = (Str(it, "size") ?? "").Trim(),
                    Sku = (Str(it, "sku") ?? "").Trim(),
                };
                if (it.TryGetProperty("discountPrice", out var dp) && dp.ValueKind == JsonValueKind.Number) vr.PriceUsd = dp.GetDecimal();
                else if (it.TryGetProperty("price", out var pr) && pr.ValueKind == JsonValueKind.Number) vr.PriceUsd = pr.GetDecimal();
                if (Str(it, "img") is string vimg && vimg.Length > 0) vr.Img = ImgBase + vimg.TrimStart('/');
                if (vr.Color.Length > 0 || vr.Size.Length > 0) e.Variants.Add(vr);
            }
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

    // Find or create a ProductAttribute (e.g. "color", "size") and one of its values, reusing existing
    // ones so imports share the same Colour/Size options as the rest of the catalogue.
    private async Task<ProductAttributeValue> AttrValueAsync(string attrSlug, string rawValue)
    {
        if (!_attrCache.TryGetValue(attrSlug, out var attr))
        {
            attr = await _db.ProductAttributes.Include(a => a.Values).FirstOrDefaultAsync(a => a.Slug == attrSlug);
            if (attr == null)
            {
                attr = new ProductAttribute { Name = Capitalize(attrSlug), Slug = attrSlug, IsActive = true };
                _db.ProductAttributes.Add(attr);
                await _db.SaveChangesAsync();
            }
            _attrCache[attrSlug] = attr;
        }

        var display = rawValue.Trim();
        var existing = attr.Values.FirstOrDefault(v => string.Equals(v.Value, display, StringComparison.OrdinalIgnoreCase))
                    ?? await _db.ProductAttributeValues.FirstOrDefaultAsync(v => v.AttributeId == attr.Id && v.Value.ToLower() == display.ToLower());
        if (existing != null) return existing;

        var val = new ProductAttributeValue { AttributeId = attr.Id, Value = display, SortOrder = attr.Values.Count + 1 };
        _db.ProductAttributeValues.Add(val);
        attr.Values.Add(val);
        await _db.SaveChangesAsync();
        return val;
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

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
