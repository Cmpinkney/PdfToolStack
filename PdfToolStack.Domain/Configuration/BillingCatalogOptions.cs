namespace PdfToolStack.Domain.Configuration;

public sealed class BillingCatalogOptions
{
    public const string SectionName = "BillingCatalog";

    public string ProductKey { get; set; } = "pdftoolstack";

    public BillingCatalogPlanOptions[] SubscriptionPlans { get; set; } = DefaultSubscriptionPlans();

    public BillingCatalogAddonOptions[] AddOns { get; set; } = DefaultAddOns();

    public IReadOnlyList<BillingCatalogPlanOptions> GetSubscriptionPlans() =>
        NormalizeSubscriptionPlans(SubscriptionPlans);

    public IReadOnlyList<BillingCatalogAddonOptions> GetAddOns() =>
        NormalizeAddOns(AddOns);

    public static BillingCatalogPlanOptions[] DefaultSubscriptionPlans() =>
        new[]
        {
            new BillingCatalogPlanOptions
            {
                Id = "monthly",
                DisplayName = "Pro Monthly",
                StripePriceKey = "ProMonthlyPriceIdV2",
                Amount = 1900,
                Label = "$19 / month",
                PlanType = "pro",
                BillingInterval = "monthly",
                ProductType = "pro_monthly",
                EntitlementType = "subscription",
                IsPublic = true
            },
            new BillingCatalogPlanOptions
            {
                Id = "yearly",
                DisplayName = "Pro Yearly",
                StripePriceKey = "ProYearlyPriceIdV2",
                Amount = 15200,
                Label = "$152 / year",
                PlanType = "pro",
                BillingInterval = "annual",
                ProductType = "pro_annual",
                EntitlementType = "subscription",
                IsPublic = true
            },
            new BillingCatalogPlanOptions
            {
                Id = "teamsMonthly",
                DisplayName = "Teams Monthly",
                StripePriceKey = "TeamsMonthlyPriceId",
                Amount = 2900,
                Label = "$29 / month",
                PlanType = "teams",
                BillingInterval = "monthly",
                ProductType = "teams_monthly",
                EntitlementType = "subscription",
                IsPublic = true
            },
            new BillingCatalogPlanOptions
            {
                Id = "teamsYearly",
                DisplayName = "Teams Yearly",
                StripePriceKey = "TeamsYearlyPriceId",
                Amount = 23200,
                Label = "$232 / year",
                PlanType = "teams",
                BillingInterval = "annual",
                ProductType = "teams_annual",
                EntitlementType = "subscription",
                IsPublic = true
            }
        };

    public static BillingCatalogAddonOptions[] DefaultAddOns() =>
        new[]
        {
            new BillingCatalogAddonOptions
            {
                Id = "largeFile",
                DisplayName = "Large File Unlock",
                StripePriceKey = "LargeFilePriceId",
                Amount = 199,
                Label = "$1.99",
                AddonType = "large_file",
                ProductType = "large_file",
                EntitlementType = "large_file_unlock",
                PurchaseType = "LargeFileUnlock",
                IsPublic = true
            },
            new BillingCatalogAddonOptions
            {
                Id = "aiDayPass",
                DisplayName = "AI Day Pass",
                StripePriceKey = "AiDayPassPriceId",
                Amount = 499,
                Label = "$4.99",
                AddonType = "ai_day_pass",
                ProductType = "ai_day_pass",
                EntitlementType = "ai_day_pass",
                PurchaseType = "AiDayPass",
                IsPublic = true
            },
            new BillingCatalogAddonOptions
            {
                Id = "aiCredits50",
                DisplayName = "AI Credit Pack",
                StripePriceKey = "AiCredits50PriceId",
                Amount = 999,
                Label = "$9.99",
                AddonType = "ai_credit_pack",
                ProductType = "ai_credit_pack",
                EntitlementType = "ai_credits",
                PurchaseType = "AiCredits",
                Credits = 50,
                IsPublic = true
            },
            new BillingCatalogAddonOptions
            {
                Id = "aiCredits200",
                DisplayName = "AI Credit Pack 200",
                StripePriceKey = "AiCredits200PriceId",
                Amount = 2999,
                Label = "$29.99",
                AddonType = "ai_credit_pack_200",
                ProductType = "ai_credit_pack_200",
                EntitlementType = "ai_credits",
                PurchaseType = "AiCredits",
                Credits = 200,
                IsPublic = false
            },
            new BillingCatalogAddonOptions
            {
                Id = "batchUnlock",
                DisplayName = "Batch Unlock",
                StripePriceKey = "BatchUnlockPriceId",
                Amount = 499,
                Label = "$4.99",
                AddonType = "batch_unlock",
                ProductType = "batch_unlock",
                EntitlementType = "batch_unlock",
                PurchaseType = "BatchUnlock",
                IsPublic = true
            }
        };

    private static IReadOnlyList<BillingCatalogPlanOptions> NormalizeSubscriptionPlans(
        IEnumerable<BillingCatalogPlanOptions>? plans)
    {
        var defaults = DefaultSubscriptionPlans()
            .ToDictionary(plan => plan.Id, StringComparer.OrdinalIgnoreCase);

        var configuredPlans = plans?
            .Where(IsUsablePlan)
            .ToArray();

        if (configuredPlans is not { Length: > 0 })
            return defaults.Values.ToArray();

        foreach (var plan in configuredPlans)
        {
            defaults[plan.Id] = defaults.TryGetValue(plan.Id, out var defaultPlan)
                ? Merge(defaultPlan, plan)
                : plan.WithDefaults();
        }

        return defaults.Values.ToArray();
    }

    private static IReadOnlyList<BillingCatalogAddonOptions> NormalizeAddOns(
        IEnumerable<BillingCatalogAddonOptions>? addOns)
    {
        var defaults = DefaultAddOns()
            .ToDictionary(addOn => addOn.Id, StringComparer.OrdinalIgnoreCase);

        var configuredAddOns = addOns?
            .Where(IsUsableAddOn)
            .ToArray();

        if (configuredAddOns is not { Length: > 0 })
            return defaults.Values.ToArray();

        foreach (var addOn in configuredAddOns)
        {
            defaults[addOn.Id] = defaults.TryGetValue(addOn.Id, out var defaultAddOn)
                ? Merge(defaultAddOn, addOn)
                : addOn.WithDefaults();
        }

        return defaults.Values.ToArray();
    }

    private static bool IsUsablePlan(BillingCatalogPlanOptions plan) =>
        !string.IsNullOrWhiteSpace(plan.Id) &&
        !string.IsNullOrWhiteSpace(plan.StripePriceKey);

    private static bool IsUsableAddOn(BillingCatalogAddonOptions addOn) =>
        !string.IsNullOrWhiteSpace(addOn.Id) &&
        !string.IsNullOrWhiteSpace(addOn.StripePriceKey) &&
        !string.IsNullOrWhiteSpace(addOn.AddonType);

    private static BillingCatalogPlanOptions Merge(
        BillingCatalogPlanOptions fallback,
        BillingCatalogPlanOptions configured) =>
        new()
        {
            Id = configured.Id.Trim(),
            DisplayName = ValueOrDefault(configured.DisplayName, fallback.DisplayName),
            StripePriceKey = ValueOrDefault(configured.StripePriceKey, fallback.StripePriceKey),
            Amount = configured.Amount > 0 ? configured.Amount : fallback.Amount,
            Label = ValueOrDefault(configured.Label, fallback.Label),
            PlanType = ValueOrDefault(configured.PlanType, fallback.PlanType),
            BillingInterval = ValueOrDefault(configured.BillingInterval, fallback.BillingInterval),
            ProductType = ValueOrDefault(configured.ProductType, fallback.ProductType),
            EntitlementType = ValueOrDefault(configured.EntitlementType, fallback.EntitlementType),
            IsPublic = configured.IsPublic
        };

    private static BillingCatalogAddonOptions Merge(
        BillingCatalogAddonOptions fallback,
        BillingCatalogAddonOptions configured) =>
        new()
        {
            Id = configured.Id.Trim(),
            DisplayName = ValueOrDefault(configured.DisplayName, fallback.DisplayName),
            StripePriceKey = ValueOrDefault(configured.StripePriceKey, fallback.StripePriceKey),
            Amount = configured.Amount > 0 ? configured.Amount : fallback.Amount,
            Label = ValueOrDefault(configured.Label, fallback.Label),
            AddonType = ValueOrDefault(configured.AddonType, fallback.AddonType),
            ProductType = ValueOrDefault(configured.ProductType, fallback.ProductType),
            EntitlementType = ValueOrDefault(configured.EntitlementType, fallback.EntitlementType),
            PurchaseType = ValueOrDefault(configured.PurchaseType, fallback.PurchaseType),
            Credits = configured.Credits ?? fallback.Credits,
            IsPublic = configured.IsPublic
        };

    private static string ValueOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class BillingCatalogPlanOptions
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string StripePriceKey { get; set; } = string.Empty;

    public int Amount { get; set; }

    public string Label { get; set; } = string.Empty;

    public string PlanType { get; set; } = "pro";

    public string BillingInterval { get; set; } = "monthly";

    public string ProductType { get; set; } = "subscription";

    public string EntitlementType { get; set; } = "subscription";

    public bool IsPublic { get; set; } = true;

    internal BillingCatalogPlanOptions WithDefaults()
    {
        DisplayName = ValueOrDefault(DisplayName, Id);
        Label = ValueOrDefault(Label, Amount > 0 ? $"${Amount / 100}" : string.Empty);
        PlanType = ValueOrDefault(PlanType, "pro");
        BillingInterval = ValueOrDefault(BillingInterval, "monthly");
        ProductType = ValueOrDefault(ProductType, "subscription");
        EntitlementType = ValueOrDefault(EntitlementType, "subscription");
        return this;
    }

    private static string ValueOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class BillingCatalogAddonOptions
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string StripePriceKey { get; set; } = string.Empty;

    public int Amount { get; set; }

    public string Label { get; set; } = string.Empty;

    public string AddonType { get; set; } = string.Empty;

    public string ProductType { get; set; } = string.Empty;

    public string EntitlementType { get; set; } = "one_time";

    public string PurchaseType { get; set; } = string.Empty;

    public int? Credits { get; set; }

    public bool IsPublic { get; set; } = true;

    internal BillingCatalogAddonOptions WithDefaults()
    {
        DisplayName = ValueOrDefault(DisplayName, Id);
        Label = ValueOrDefault(Label, Amount > 0 ? $"${Amount / 100}" : string.Empty);
        ProductType = ValueOrDefault(ProductType, AddonType);
        EntitlementType = ValueOrDefault(EntitlementType, "one_time");
        return this;
    }

    private static string ValueOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
