using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NativeBilling;

public enum NativeMembershipStore
{
  AppleIos,
  AppleMacOs,
  GooglePlay,
  MicrosoftStore,
  MacCatalyst,
}

public enum MembershipPurchaseRefusal
{
  Conflict,
  StripeCheckout,
  NoStoreRow,
  PlayLaunchMismatch,
  ReplacementUnavailable,
}

public sealed record NativeMembershipStoreRow(
    string CanonicalProductId,
    string Plan,
    string Provider,
    string ApplicationId,
    string StoreProductId,
    string? BasePlanId,
    string? SkuId);

public static class NativeMembershipCatalog
{
  public const string AppleIosApplication = "ai.voucha.ios";
  public const string AppleMacOsApplication = "ai.voucha.macos";
  public const string AndroidApplication = "ai.voucha.android";

  public static IReadOnlyList<NativeMembershipStoreRow> Rows(
      IReadOnlyList<MembershipCatalogProduct> products,
      NativeMembershipStore store)
  {
    ArgumentNullException.ThrowIfNull(products);
    return products.SelectMany(product => product.Providers
        .Where(provider => Matches(provider, store))
        .Select(provider => new NativeMembershipStoreRow(
            product.Id,
            product.Plan,
            provider.Provider,
            provider.ApplicationId,
            provider.ProductId,
            provider.BasePlanId,
            provider.SkuId)))
        .ToArray();
  }

  public static MembershipPurchaseRefusal? Refusal(
      NativeMembershipPurchaseIntent intent,
      IReadOnlyList<MembershipCatalogProduct> products,
      NativeMembershipStore store,
      NativeMembershipOverview overview)
  {
    ArgumentNullException.ThrowIfNull(intent);
    ArgumentNullException.ThrowIfNull(products);
    ArgumentNullException.ThrowIfNull(overview);
    var provider = intent.Provider;
    var launch = intent.Launch;
    if (provider == "stripe" || LaunchString(launch, "kind") == "stripe_checkout")
    {
      return MembershipPurchaseRefusal.StripeCheckout;
    }

    if (!SurfaceAccepts(provider, store))
    {
      return MembershipPurchaseRefusal.NoStoreRow;
    }

    if (overview.BlocksStoreReplacement)
    {
      return MembershipPurchaseRefusal.ReplacementUnavailable;
    }

    var matched = Rows(products, store).Where(row => row.CanonicalProductId == intent.ProductId).ToArray();
    if (matched.Length != 1)
    {
      return MembershipPurchaseRefusal.NoStoreRow;
    }

    var row = matched[0];
    if (store == NativeMembershipStore.GooglePlay && !PlayLaunchMatches(row, launch))
    {
      return MembershipPurchaseRefusal.PlayLaunchMismatch;
    }

    if (store == NativeMembershipStore.AppleIos && LaunchString(launch, "product_id") != row.StoreProductId)
    {
      return MembershipPurchaseRefusal.NoStoreRow;
    }

    return null;
  }

  private static bool Matches(MembershipCatalogProvider provider, NativeMembershipStore store) => store switch
  {
    NativeMembershipStore.AppleIos => provider.Provider == "apple_app_store" &&
        provider.ApplicationId == AppleIosApplication,
    NativeMembershipStore.AppleMacOs => provider.Provider == "apple_app_store" &&
        provider.ApplicationId == AppleMacOsApplication,
    NativeMembershipStore.GooglePlay => provider.Provider == "google_play" &&
        provider.ApplicationId == AndroidApplication,
    NativeMembershipStore.MicrosoftStore => provider.Provider == "microsoft_store",
    _ => false,
  };

  private static bool SurfaceAccepts(string provider, NativeMembershipStore store) => store switch
  {
    NativeMembershipStore.AppleIos or NativeMembershipStore.AppleMacOs => provider == "apple_app_store",
    NativeMembershipStore.GooglePlay => provider == "google_play",
    NativeMembershipStore.MicrosoftStore => provider == "microsoft_store",
    _ => false,
  };

  private static bool PlayLaunchMatches(NativeMembershipStoreRow row, JsonElement launch) =>
      LaunchString(launch, "product_id") == row.StoreProductId &&
      LaunchString(launch, "base_plan_id") == row.BasePlanId;

  private static string? LaunchString(JsonElement launch, string name)
  {
    if (launch.ValueKind != JsonValueKind.Object ||
        !launch.TryGetProperty(name, out var value) ||
        value.ValueKind != JsonValueKind.String)
    {
      return null;
    }

    return value.GetString();
  }
}
