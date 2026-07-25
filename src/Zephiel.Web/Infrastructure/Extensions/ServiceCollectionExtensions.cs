using Microsoft.Extensions.Caching.Memory;
using Zephiel.Web.Services;
using Zephiel.Web.Services.Payment;

namespace Zephiel.Web.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZephielServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Inventory ───────────────────────────────────────────────────────
        services.AddMemoryCache();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IOrderNumberService, OrderNumberService>();
        services.AddScoped<SeoDescriptionGenerator>();
        services.AddScoped<JournalPostGenerator>();
        services.AddScoped<IOrderFulfilmentService, OrderFulfilmentService>();
        services.AddScoped<ITransferWorkflowService, TransferWorkflowService>();

        // ─── Merchandising (best sellers / trending / new arrivals / recently viewed) ──
        services.AddScoped<IMerchandisingService, MerchandisingService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IGiftCardService, GiftCardService>();
        services.AddScoped<IStorefrontCache, StorefrontCache>();

        // ─── Logistics (Lagos delivery) integration — order push + delivered callback ──
        services.Configure<Zephiel.Web.Services.Logistics.LogisticsOptions>(configuration.GetSection("Logistics"));
        services.AddHttpClient("logistics", c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<Zephiel.Web.Services.Logistics.ILogisticsDispatchService, Zephiel.Web.Services.Logistics.LogisticsDispatchService>();

        // ─── Marketing (campaigns / audiences) ────────────────────────────────
        services.AddScoped<Zephiel.Web.Services.Marketing.IMarketingService, Zephiel.Web.Services.Marketing.MarketingService>();
        services.AddScoped<Zephiel.Web.Services.Marketing.IMarketingAttributionService, Zephiel.Web.Services.Marketing.MarketingAttributionService>();
        services.AddScoped<IReferralService, ReferralService>();
        services.AddSingleton<IManifestTokenService, ManifestTokenService>();
        services.AddScoped<Zephiel.Web.Services.Social.ISocialPublisher, Zephiel.Web.Services.Social.NullSocialPublisher>();

        // ─── WhatsApp (Business API via Twilio; provider-agnostic) ────────────
        // Credentials read at request time from Settings (Admin → Integrations), so entering them
        // takes effect immediately. Own HttpClient with a short timeout.
        services.AddHttpClient<IWhatsAppService, WhatsAppService>(c => c.Timeout = TimeSpan.FromSeconds(20));

        // ─── Store-level authorization (writes-only) ──────────────────────────
        services.AddScoped<IStoreAccessService, StoreAccessService>();

        // ─── Product Import (WooCommerce CSV) ─────────────────────────────────
        services.AddScoped<IWooCommerceImportService, WooCommerceImportService>();
        services.AddScoped<ICatalogImportService, CatalogImportService>();

        // ─── Site Settings ────────────────────────────────────────────────────
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<DeliveryZoneService>();

        // ─── Audit Log ────────────────────────────────────────────────────────
        services.AddScoped<IAuditService, AuditService>();

        // ─── Roles & Permissions ───────────────────────────────────────────────
        services.AddScoped<IPermissionService, PermissionService>();

        // ─── Discounts ──────────────────────────────────────────────────────────
        services.AddScoped<IDiscountService, DiscountService>();

        // ─── Payment ─────────────────────────────────────────────────────────
        // Keys and the active provider are read at request time from Settings (Admin → Integrations),
        // falling back to config. PaymentRouter picks the provider per call and each provider service
        // applies its current key per call, so entering keys in the admin takes effect immediately —
        // no redeploy, no restart. All three providers are registered; the router selects one.
        services.AddScoped<PaymentCredentials>();
        services.AddHttpClient<PaystackPaymentService>();
        services.AddHttpClient<FlutterwavePaymentService>();
        services.AddScoped<StripePaymentService>();
        services.AddScoped<IPaymentService, PaymentRouter>();
        // Subscription (API-connector) billing — the developer's own Paystack account.
        services.AddHttpClient<ISubscriptionPaymentService, SubscriptionPaymentService>();

        return services;
    }
}
