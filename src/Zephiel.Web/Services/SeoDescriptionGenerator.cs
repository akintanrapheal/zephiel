namespace Zephiel.Web.Services;

/// <summary>Builds unique, SEO-friendly rich-text (HTML) product descriptions from a product's name
/// and category. It first works out what <em>kind</em> of product this is — jewellery vs. an accessory
/// such as a bag/clutch, belt, cap/scarf, sunglasses, watch, hair accessory, keyring or gift packaging —
/// so accessories are never described as jewellery (no "AAA cubic zirconia" or brass-plating language on a
/// cap or belt). Jewellery still gets style/finish/adornment detection plus the jewellery-care section;
/// accessories get material, feature and care copy that actually fits them. Output is sanitised by
/// ProductHtml before saving and is fully editable afterwards in the product editor.</summary>
public class SeoDescriptionGenerator
{
    // ─── What kind of product is this? ────────────────────────────────────────
    public enum ProductKind
    {
        Jewellery, Bag, Belt, CapScarf, Eyewear, Watch, HairAccessory, KeyRing, GiftPackaging, CareProduct, Accessory
    }

    /// <summary>Classify by category first, then by name. Order matters: e.g. "key ring" and "bracelet
    /// watch" must be caught before the jewellery "ring"/"bracelet" keywords.</summary>
    public static ProductKind DetectKind(string name, string category)
    {
        var n = (name ?? "").ToLowerInvariant();
        var c = (category ?? "").ToLowerInvariant();
        bool In(string s, params string[] kw) { foreach (var k in kw) if (s.Contains(k)) return true; return false; }
        bool Either(params string[] kw) => In(c, kw) || In(n, kw);

        // Watches (incl. "bracelet watch" / "strap watch") — a watch is not jewellery.
        if (Either("watch")) return ProductKind.Watch;
        // Eyewear
        if (Either("sunglass", "eyewear", "shades") || In(n, "glasses")) return ProductKind.Eyewear;
        // Bags / clutches / purses / wallets
        if (Either("clutch", "purse", "wallet", "handbag") || In(n, "bag", "pouch")) return ProductKind.Bag;
        // Belts (waist/trouser *chains* are jewellery and handled below — they don't contain "belt")
        if (Either("belt")) return ProductKind.Belt;
        // Caps, hats & scarves
        if (Either("cap", "scarf", "hat", "beanie", "durag", "headwrap", "bandana")) return ProductKind.CapScarf;
        // Hair accessories
        if (In(c, "hair") || In(n, "hair clip", "scrunchie", "headband", "hair band", "hairband", "hair pin", "barrette", "hair claw")) return ProductKind.HairAccessory;
        // Keyrings / keychains (before jewellery "ring")
        if (Either("key ring", "keyring", "keychain", "key chain", "key holder")) return ProductKind.KeyRing;
        // Care solutions / cleaners
        if (In(c, "care solution", "cleaning") || In(n, "cleaner", "polish", "care solution", "cleaning cloth", "polishing")) return ProductKind.CareProduct;
        // Gift packaging & storage (boxes, organisers, combo/gift packs)
        if (Either("gift box", "gift boxes", "storage", "organiser", "organizer", "combo", "package", "gift set") || In(n, "gift box", "storage box")) return ProductKind.GiftPackaging;

        // Jewellery — by category then by name.
        if (Either("earring", "necklace", "pendant", "anklet", "ring", "bangle", "bracelet", "brooch",
                   "cufflink", "chain", "extender", "set", "bead", "charm", "jewel"))
            return ProductKind.Jewellery;

        // Unknown category (e.g. "Accessories", "Mens"): default to a generic accessory rather than
        // risk describing a non-jewellery item with jewellery copy.
        return ProductKind.Accessory;
    }

    public string Build(int seed, string name, string category)
    {
        var kind = DetectKind(name, category);
        return kind == ProductKind.Jewellery
            ? BuildJewellery(seed, name, category)
            : BuildAccessory(seed, name, kind);
    }

    public string BuildShort(int seed, string name, string category)
    {
        var kind = DetectKind(name, category);
        return kind == ProductKind.Jewellery
            ? BuildJewelleryShort(seed, name, category)
            : BuildAccessoryShort(seed, name, kind);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  JEWELLERY
    // ══════════════════════════════════════════════════════════════════════════
    private static readonly string[] JewelleryIntros =
    {
        "The <strong>{0}</strong> is a modern everyday staple &mdash; {1} {2} with a quiet, lasting polish that carries you from desk to dinner.",
        "Clean, confident and built to last, the <strong>{0}</strong> is {1} {2} you can genuinely wear every single day.",
        "Meet the <strong>{0}</strong> &mdash; {1} {2} that stays bright, shrugs off tarnish and feels featherlight to wear.",
        "Understated but never plain, the <strong>{0}</strong> brings a refined {1} {2} finish to whatever is already in your wardrobe.",
        "The <strong>{0}</strong> proves everyday can be elevated: {1} {2} made to be worn, not saved for &lsquo;best&rsquo;.",
        "Effortless and enduring, the <strong>{0}</strong> is {1} {2} &mdash; the kind of piece you reach for on repeat.",
    };

    private string BuildJewellery(int seed, string name, string category)
    {
        var n = (name ?? "").ToLowerInvariant();
        var c = (category ?? "").ToLowerInvariant();
        var safeName = System.Net.WebUtility.HtmlEncode(name ?? "");

        var (noun, style, closeLabel, closeVal) = Categorize(n, c);
        var fin = Finish(n);
        var pearl = n.Contains("pearl");
        var stone = ContainsAny(n, "stone", "crystal", "sparkle", "shiny", "gloss", "zirconia", "laser",
            "shimmer", "bloom", "royal", "glow", "diamond", "ice", "star");
        var typPhrase = string.IsNullOrEmpty(style) ? noun : $"{style} {noun}";
        var plated = fin != "silver-tone";

        var lead = string.Format(JewelleryIntros[Math.Abs(seed) % JewelleryIntros.Length], safeName, fin, typPhrase);
        lead += pearl
            ? " Soft faux pearls add a feminine, timeless glow."
            : stone
                ? " Hand-set cubic zirconia catches the light from every angle."
                : " A clean silhouette that layers effortlessly with the rest of your jewellery.";

        // At-a-glance facts
        var facts = new List<string>
        {
            $"<li><strong>Material</strong> &mdash; {(plated ? $"316L stainless steel, {fin} PVD-plated" : "polished 316L stainless steel")}</li>"
        };
        if (pearl) facts.Add("<li><strong>Stones</strong> &mdash; hand-set faux pearls</li>");
        else if (stone) facts.Add("<li><strong>Stones</strong> &mdash; AAA-grade cubic zirconia</li>");
        if (!string.IsNullOrEmpty(closeLabel)) facts.Add($"<li><strong>{closeLabel}</strong> &mdash; {closeVal}</li>");
        facts.Add("<li><strong>Skin-friendly</strong> &mdash; hypoallergenic, nickel &amp; lead free</li>");
        facts.Add("<li><strong>Wear</strong> &mdash; tarnish- &amp; water-resistant, made for every day</li>");
        facts.Add($"<li><strong>Style</strong> &mdash; {typPhrase}</li>");

        var material = plated
            ? $"Built on 316L surgical-grade stainless steel and finished with a bonded {fin} PVD coating, so the colour won&rsquo;t flake, fade or turn your skin green. It resists rust and tarnish, stands up to water and daily wear, and stays gentle on sensitive skin."
            : "Made from 316L surgical-grade stainless steel &mdash; the same metal used for medical tools. It won&rsquo;t rust, tarnish or irritate sensitive skin, and holds its bright, polished shine through everyday wear and water.";

        var care = new List<string>
        {
            "Rinse with warm water and a drop of mild soap, then dry with a soft cloth &mdash; no special cleaner needed.",
            "Happy around water and sweat; a quick wipe after wear keeps it gleaming.",
        };
        if (plated) care.Add("To protect the plated colour, keep it away from chlorine, bleach and abrasive polishes, and spray perfume before putting it on.");
        care.Add("Store in a pouch or box so it doesn&rsquo;t scratch against other pieces.");
        care.Add("No need to save it for &lsquo;best&rsquo; &mdash; it&rsquo;s built to be worn.");

        return $"<p>{lead}</p>"
             + "<h3>At a glance</h3><ul>" + string.Concat(facts) + "</ul>"
             + "<h3>Styling notes</h3><p>" + StylingNote(noun) + "</p>"
             + "<h3>Why stainless steel</h3><p>" + material + "</p>"
             + "<h3>Looking after it</h3><ul>" + string.Concat(care.ConvertAll(x => $"<li>{x}</li>")) + "</ul>";
    }

    private static string StylingNote(string noun) => noun switch
    {
        "earrings"      => "Wear them on their own for a polished daytime look, or pair with smaller studs for an easy, collected ear.",
        "necklace"      => "Layer it over shorter chains for depth, or let it sit alone against a plain neckline.",
        "ring"          => "Wear it solo as a quiet statement, or stack it with slimmer bands for a modern, mismatched finish.",
        "bangle" or "bracelet" => "Stack it with a watch or a couple of finer bracelets for an effortless, put-together wrist.",
        "anklet"        => "Made for bare skin in warmer months &mdash; wear one or layer a few for a laid-back finish.",
        "brooch"        => "Pin it to a lapel, a coat or a bag strap to lift an otherwise simple outfit.",
        "jewellery set" => "Designed to be worn together for a coordinated look &mdash; or split the pieces across different outfits.",
        _               => "Dress it up for occasions or keep it low-key for everyday &mdash; it works both ways.",
    };

    private static readonly string[] JewelleryShorts =
    {
        "Everyday {0} {1}{2} in hypoallergenic 316L stainless steel — tarnish-proof and water-friendly.",
        "Polished {0} {1}{2} — stainless steel that won't fade, rust or irritate sensitive skin.",
        "Handcrafted {0} {1}{2} in surgical-grade stainless steel, made to keep its shine wear after wear.",
    };

    private string BuildJewelleryShort(int seed, string name, string category)
    {
        var n = (name ?? "").ToLowerInvariant();
        var c = (category ?? "").ToLowerInvariant();
        var (noun, style, _, _) = Categorize(n, c);
        var fin = Finish(n);
        var typ = string.IsNullOrEmpty(style) ? noun : $"{style} {noun}";
        var adorn = n.Contains("pearl") ? " with faux pearls"
            : ContainsAny(n, "stone", "crystal", "sparkle", "shiny", "gloss", "zirconia", "laser",
                "shimmer", "bloom", "royal", "glow", "diamond", "ice", "star") ? " with cubic zirconia"
            : "";
        return string.Format(JewelleryShorts[Math.Abs(seed) % JewelleryShorts.Length], fin, typ, adorn);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ACCESSORIES (non-jewellery)
    // ══════════════════════════════════════════════════════════════════════════
    private static readonly string[] AccessoryIntros =
    {
        "The <strong>{0}</strong> is {1} &mdash; an easy finishing touch that pulls a look together without trying too hard.",
        "Meet the <strong>{0}</strong>: {1} that blends everyday practicality with a genuinely stylish finish.",
        "Understated and useful in equal measure, the <strong>{0}</strong> is {1} made to earn its place in your rotation.",
        "The <strong>{0}</strong> is {1} &mdash; quality made, quietly stylish and ready for daily use.",
    };

    private sealed class AccSpec
    {
        public string Noun = "accessory";
        public string Descriptor = "a stylish accessory";
        public string IntroTail = "";
        public List<string> Bullets = new();
        public List<(string Label, string Val)> Details = new();
        public string CareTitle = "Care Instructions";
        public List<string> Care = new();
        public string ShortLine = "";
    }

    private string BuildAccessory(int seed, string name, ProductKind kind)
    {
        var n = (name ?? "").ToLowerInvariant();
        var safeName = System.Net.WebUtility.HtmlEncode(name ?? "");
        var spec = SpecFor(kind, n);

        var lead = string.Format(AccessoryIntros[Math.Abs(seed) % AccessoryIntros.Length], safeName, spec.Descriptor);
        if (!string.IsNullOrEmpty(spec.IntroTail)) lead += " " + spec.IntroTail;

        var facts = new List<string>();
        foreach (var (label, val) in spec.Details) facts.Add($"<li><strong>{label}</strong> &mdash; {val}</li>");
        facts.Add($"<li><strong>Type</strong> &mdash; {spec.Noun}</li>");

        return $"<p>{lead}</p>"
             + "<h3>At a glance</h3><ul>" + string.Concat(facts) + "</ul>"
             + "<h3>Highlights</h3><ul>" + string.Concat(spec.Bullets.ConvertAll(b => $"<li>{b}</li>")) + "</ul>"
             + "<h3>Looking after it</h3><ul>" + string.Concat(spec.Care.ConvertAll(x => $"<li>{x}</li>")) + "</ul>";
    }

    private string BuildAccessoryShort(int seed, string name, ProductKind kind)
    {
        var n = (name ?? "").ToLowerInvariant();
        var spec = SpecFor(kind, n);
        return spec.ShortLine;
    }

    /// <summary>Per-kind copy so accessories read naturally and never borrow jewellery language.</summary>
    private static AccSpec SpecFor(ProductKind kind, string n)
    {
        bool Has(params string[] kw) { foreach (var k in kw) if (n.Contains(k)) return true; return false; }
        var s = new AccSpec();

        switch (kind)
        {
            case ProductKind.Bag:
            {
                var embellished = Has("stone", "stoned", "crystal", "diamante", "sparkle", "embellish", "beaded", "pearl", "sequin");
                var noun = Has("wallet") ? "wallet" : Has("purse") ? "purse" : Has("handbag") ? "handbag"
                    : Has("bag") && !Has("clutch") ? "bag" : "clutch bag";
                var closure = noun == "wallet" ? "snap closure" : noun is "clutch bag" or "purse" ? "magnetic clasp" : "zip closure";
                s.Noun = noun;
                s.Descriptor = $"a {(embellished ? "beautifully embellished" : "sleek")} {noun}";
                s.IntroTail = embellished
                    ? "Its crystal detailing catches the light beautifully, making it the perfect finishing touch for weddings and evening events."
                    : "Roomy enough for your essentials and finished to complement both daytime and evening looks.";
                s.Bullets = new()
                {
                    "Spacious enough for your phone, cards and everyday essentials",
                    $"Secure <strong>{closure}</strong> keeps your belongings safe",
                };
                if (noun is "clutch bag" or "purse" or "handbag") s.Bullets.Add("Detachable chain strap &mdash; carry it in hand or over the shoulder");
                if (embellished) s.Bullets.Add("Eye-catching crystal embellishment for a glamorous finish");
                s.Bullets.Add("Perfect for weddings, parties and special occasions");
                s.Details = new()
                {
                    ("Material", embellished ? "satin-finish body with sparkling crystal detailing" : "premium faux leather"),
                    ("Closure", closure),
                };
                if (noun is "clutch bag" or "purse" or "handbag") s.Details.Add(("Strap", "detachable chain strap"));
                s.CareTitle = "Accessory Care";
                s.Care = new()
                {
                    "Wipe gently with a soft, dry cloth &mdash; avoid water and harsh cleaners.",
                    "Keep away from perfume, lotion and moisture.",
                    "Store in a dust bag, out of direct sunlight, to keep it looking new.",
                };
                if (embellished) s.Care.Add("Handle the embellishments with care to keep every stone in place.");
                s.ShortLine = $"A {(embellished ? "crystal-embellished" : "chic")} {noun} from Zephiel &mdash; roomy, secure and perfect for special occasions.";
                break;
            }

            case ProductKind.Belt:
            {
                var leather = Has("leather") && !Has("faux");
                s.Noun = "belt";
                s.Descriptor = $"a versatile {(leather ? "genuine leather" : "premium faux-leather")} belt";
                s.IntroTail = "Adjustable to your perfect fit, it pulls any outfit together &mdash; from tailored looks to casual denim.";
                s.Bullets = new()
                {
                    "Adjustable fit for all-day comfort",
                    "Sturdy, secure buckle that holds firm",
                    "Smart, versatile design for both formal and casual outfits",
                    "A timeless wardrobe staple",
                };
                s.Details = new()
                {
                    ("Material", leather ? "genuine leather" : "premium faux leather"),
                    ("Buckle", "polished alloy buckle"),
                    ("Fit", "adjustable"),
                };
                s.CareTitle = "Care Instructions";
                s.Care = new()
                {
                    "Wipe clean with a soft, dry cloth.",
                    "Keep away from prolonged moisture and direct heat.",
                    "Store rolled or hung to keep its shape.",
                };
                s.ShortLine = $"A versatile {(leather ? "genuine leather" : "faux-leather")} belt from Zephiel &mdash; adjustable, sturdy and made to last.";
                break;
            }

            case ProductKind.CapScarf:
            {
                var scarf = Has("scarf");
                s.Noun = scarf ? "scarf" : "cap";
                s.Descriptor = scarf ? "a soft, elegant scarf" : "a comfortable everyday cap";
                s.IntroTail = scarf
                    ? "Drape, wrap or knot it to add colour and warmth to any outfit."
                    : "One-size comfort with an adjustable fit, ready for casual days out.";
                s.Bullets = scarf
                    ? new() { "Soft, lightweight fabric that feels great against the skin", "Versatile &mdash; style it round the neck, over the shoulders or as a wrap", "Adds colour and finish to any outfit", "Easy to fold and carry" }
                    : new() { "Breathable, comfortable fabric", "Adjustable fit suits most head sizes", "Casual, everyday style", "Great for men and women" };
                s.Details = new()
                {
                    ("Material", scarf ? "soft, lightweight woven fabric" : "breathable cotton-blend fabric"),
                    ("Fit", scarf ? "one size" : "adjustable, one size fits most"),
                };
                s.CareTitle = "Care Instructions";
                s.Care = new()
                {
                    "Gentle hand wash or cold machine wash.",
                    "Do not bleach.",
                    "Air dry and reshape while damp.",
                };
                s.ShortLine = scarf
                    ? "A soft, versatile scarf from Zephiel &mdash; lightweight and easy to style."
                    : "A comfortable, adjustable cap from Zephiel &mdash; a casual everyday staple.";
                break;
            }

            case ProductKind.Eyewear:
            {
                s.Noun = "sunglasses";
                s.Descriptor = "a pair of stylish sunglasses";
                s.IntroTail = "Shield your eyes in style &mdash; a must-have finishing touch for sunny days.";
                s.Bullets = new()
                {
                    "UV-protective tinted lenses",
                    "Lightweight frame for comfortable all-day wear",
                    "On-trend silhouette that flatters most face shapes",
                    "Protective case included",
                };
                s.Details = new()
                {
                    ("Frame", "lightweight moulded frame"),
                    ("Lenses", "UV-protective tinted lenses"),
                };
                s.CareTitle = "Care Instructions";
                s.Care = new()
                {
                    "Clean the lenses with a soft microfibre cloth.",
                    "Store in the protective case when not in use.",
                    "Avoid leaving them in extreme heat, such as a hot car.",
                };
                s.ShortLine = "Stylish UV-protective sunglasses from Zephiel &mdash; lightweight, on-trend and case included.";
                break;
            }

            case ProductKind.Watch:
            {
                var band = Has("bracelet") ? "bracelet" : "strap";
                s.Noun = $"{band} watch";
                s.Descriptor = $"an elegant {band} watch";
                s.IntroTail = "Reliable timekeeping meets standout style on your wrist.";
                s.Bullets = new()
                {
                    "Precise quartz movement for reliable timekeeping",
                    "Adjustable band for a comfortable fit",
                    "Clear, easy-to-read dial",
                    "Versatile day-to-night design",
                };
                s.Details = new()
                {
                    ("Movement", "quartz"),
                    ("Case", "polished alloy"),
                    ("Band", $"{band} band"),
                };
                s.CareTitle = "Watch Care";
                s.Care = new()
                {
                    "Keep away from water and moisture unless it is marked water-resistant.",
                    "Avoid strong magnets and knocks.",
                    "Wipe the case and band with a soft, dry cloth.",
                };
                s.ShortLine = $"An elegant {band} watch from Zephiel &mdash; reliable quartz movement and a versatile, everyday design.";
                break;
            }

            case ProductKind.HairAccessory:
            {
                var noun = Has("headband") ? "headband" : Has("scrunchie") ? "scrunchie"
                    : Has("claw") ? "hair claw" : Has("clip") ? "hair clip" : "hair accessory";
                s.Noun = noun;
                s.Descriptor = $"a chic {noun}";
                s.IntroTail = "A pretty, practical way to finish your hairstyle.";
                s.Bullets = new()
                {
                    "Secure, comfortable hold",
                    "Gentle on hair",
                    "Comfortable enough for all-day wear",
                    "Versatile &mdash; dresses up or down with any look",
                };
                s.Details = new() { ("Material", "quality, hair-friendly materials") };
                s.CareTitle = "Care Instructions";
                s.Care = new()
                {
                    "Wipe clean and keep dry.",
                    "Store flat, away from direct heat.",
                };
                s.ShortLine = $"A chic {noun} from Zephiel &mdash; a secure, gentle finishing touch for any hairstyle.";
                break;
            }

            case ProductKind.KeyRing:
            {
                var charm = Has("charm", "pendant", "tassel");
                s.Noun = "keyring";
                s.Descriptor = $"a handy decorative keyring{(charm ? " with a charm" : "")}";
                s.IntroTail = "Keep your keys organised with a little extra personality &mdash; it makes a lovely small gift, too.";
                s.Bullets = new()
                {
                    "Sturdy split ring holds keys securely",
                    charm ? "Eye-catching decorative charm" : "Neat, durable design",
                    "A thoughtful, affordable gift",
                    "Built for everyday use",
                };
                s.Details = new() { ("Material", $"durable metal{(charm ? " with a decorative charm" : "")}") };
                s.CareTitle = "Care Instructions";
                s.Care = new() { "Wipe with a soft cloth.", "Keep dry to avoid tarnishing." };
                s.ShortLine = "A handy decorative keyring from Zephiel &mdash; sturdy, stylish and a great little gift.";
                break;
            }

            case ProductKind.GiftPackaging:
            {
                var box = Has("box", "storage", "case", "organis", "organiz");
                s.Noun = box ? "jewellery box" : "gift set";
                s.Descriptor = $"a beautifully presented {s.Noun}";
                s.IntroTail = "Thoughtfully designed to protect and present your pieces &mdash; ready for gifting.";
                s.Bullets = new()
                {
                    box ? "Keeps your jewellery organised and protected" : "Beautifully presented and ready to gift",
                    "Elegant presentation",
                    "Makes a thoughtful gift",
                    "Practical, everyday use",
                };
                s.Details = new() { ("Material", "quality materials") };
                s.CareTitle = "Care Instructions";
                s.Care = new() { "Keep dry and wipe clean with a soft cloth.", "Store away from direct sunlight." };
                s.ShortLine = $"A beautifully presented {s.Noun} from Zephiel &mdash; practical, elegant and ready to gift.";
                break;
            }

            case ProductKind.CareProduct:
            {
                s.Noun = "jewellery care product";
                s.Descriptor = "an easy-to-use jewellery care product";
                s.IntroTail = "Keep your Zephiel pieces looking their sparkling best.";
                s.Bullets = new()
                {
                    "Gentle on plated and costume jewellery",
                    "Helps restore shine and remove dullness",
                    "Simple to use at home",
                    "Keeps your pieces looking new for longer",
                };
                s.Details = new() { ("Use", "for gold/silver-plated and costume jewellery") };
                s.CareTitle = "How to Use";
                s.Care = new()
                {
                    "Follow the directions on the pack.",
                    "Use gently and avoid over-rubbing plated finishes.",
                    "Store in a cool, dry place.",
                };
                s.ShortLine = "An easy-to-use jewellery care product from Zephiel &mdash; keeps your pieces sparkling.";
                break;
            }

            default: // Accessory (generic / unknown)
            {
                s.Noun = "accessory";
                s.Descriptor = "a stylish accessory";
                s.IntroTail = "A versatile finishing touch that works with any outfit.";
                s.Bullets = new()
                {
                    "Versatile, easy-to-style design",
                    "Quality made to last",
                    "Great for everyday wear or gifting",
                    "A smart finishing touch for any look",
                };
                s.Details = new() { ("Material", "quality materials") };
                s.CareTitle = "Care Instructions";
                s.Care = new() { "Wipe clean with a soft, dry cloth.", "Keep away from moisture and direct heat.", "Store in a cool, dry place." };
                s.ShortLine = "A stylish, quality-made accessory from Zephiel &mdash; versatile and perfect for gifting.";
                break;
            }
        }
        return s;
    }

    // ─── Jewellery sub-typing / finish helpers (unchanged) ─────────────────────
    private static (string noun, string style, string closeLabel, string closeVal) Categorize(string n, string c)
    {
        string Pick(params (string kw, string val)[] m)
        {
            foreach (var (kw, val) in m) if (n.Contains(kw)) return val;
            return "";
        }

        if (c.Contains("earring"))
        {
            var st = Pick(("stud", "stud"), ("hoop", "hoop"), ("loop", "hoop"), ("orbit", "hoop"), ("spiral", "hoop"),
                ("curl", "hoop"), ("coil", "hoop"), ("huggie", "hoop"), ("drop", "drop"), ("drape", "drop"),
                ("teardrop", "drop"), ("tassel", "drop"), ("dangle", "drop"), ("chandelier", "drop"));
            var close = st == "stud" ? "secure stud post with butterfly backs"
                : st == "drop" ? "easy hook fastening"
                : st == "hoop" ? "comfortable hinged fastening" : "secure fastening";
            return ("earrings", st, "Fastening", close);
        }
        if (c.Contains("necklace"))
            return ("necklace", Pick(("choker", "choker"), ("pendant", "pendant"), ("layered", "layered"),
                ("layer", "layered"), ("chain", "chain")), "Chain", "adjustable chain with a secure lobster clasp");
        if (c.Contains("anklet"))
            return ("anklet", Pick(("layered", "layered"), ("layer", "layered"), ("ball", "beaded"),
                ("bead", "beaded"), ("stone", "stone")), "Chain", "adjustable chain with a secure clasp");
        if (c.Contains("ring"))
            return ("ring", Pick(("band", "band"), ("stack", "stacking"), ("statement", "statement")), "Fit", "comfortable, true-to-size band");
        if (c.Contains("bangle") || c.Contains("bracelet"))
        {
            var noun = n.Contains("bangle") ? "bangle" : n.Contains("bracelet") ? "bracelet" : (c.Contains("bangle") ? "bangle" : "bracelet");
            var st = Pick(("cuff", "cuff"), ("tennis", "tennis"), ("beaded", "beaded"), ("bead", "beaded"),
                ("chain", "chain"), ("charm", "charm"));
            return (noun, st, "Fit", "comfortable, easy-to-wear fit");
        }
        if (c.Contains("brooch")) return ("brooch", Pick(("pin", "pin")), "Fastening", "secure pin fastening");
        if (c.Contains("cufflink")) return ("cufflinks", "", "Fastening", "secure swivel-bar fastening");
        if (c.Contains("chain")) return ("chain", Pick(("waist", "waist"), ("trouser", "trouser"), ("body", "body")), "Chain", "adjustable chain with a secure clasp");
        if (c.Contains("set")) return ("jewellery set", "coordinated", "", "");
        return ("piece", "", "", "");
    }

    private static string Finish(string n)
    {
        if (n.Contains("rose")) return "rose-gold";
        if (n.Contains("silver") || n.Contains("white")) return "silver-tone";
        if (n.Contains("black")) return "sleek black";
        if (ContainsAny(n, "2tone", "3tone", "2 tone", "3 tone", "tone", "mix", "multi", "colour", "color")) return "mixed-metal";
        return "gold-tone";
    }

    private static bool ContainsAny(string n, params string[] kws)
    {
        foreach (var k in kws) if (n.Contains(k)) return true;
        return false;
    }
}
