using Voucha.Client.Core.Api;
using Voucha.Client.Core.NativeBilling;
using Xunit;

namespace Voucha.Client.Core.Tests.NativeBilling;

public sealed class NativeMembershipOverviewTests
{
  [Fact]
  public void DefaultOverviewRoundTripsAndStaysFree()
  {
    var overview = MembershipFixture.Decode<NativeMembershipOverview>("native.memberships.me.default");
    Assert.Null(overview.Membership);
    Assert.Equal("free", overview.VisiblePlan);
    Assert.False(overview.BlocksStoreReplacement);
    Assert.Equal(0, overview.Pending.Grants);
    MembershipFixture.AssertRoundTrip<NativeMembershipOverview>("native.memberships.me.default");
  }

  [Fact]
  public void LifecycleOverviewKeepsIneffectiveSourcesAndPendingWork()
  {
    var overview = MembershipFixture.Decode<NativeMembershipOverview>("native.memberships.me.lifecycle.default");
    Assert.Null(overview.Membership);
    Assert.Equal("free", overview.VisiblePlan);
    Assert.True(overview.BlocksStoreReplacement);
    Assert.Equal(1, overview.Pending.Grants);
    Assert.Equal(["automatic_refund"], overview.Pending.FinancialOperations.Select(item => item.Kind));
    Assert.Equal("2027-01-01T00:00:00.000Z", overview.Pending.Switches[0].EligibleAt);
    Assert.Equal(
        ["competing_direct_source", "wrong_account"],
        overview.Pending.Verifications.Select(item => item.ReasonCode));
    Assert.Equal([false, false], overview.Sources.Select(source => source.IsEffective));
    Assert.Null(overview.Sources[1].Renewal?.Price);
    Assert.Equal("apple_subscriptions", overview.Management?.Destination);
    MembershipFixture.AssertRoundTrip<NativeMembershipOverview>("native.memberships.me.lifecycle.default");
  }

  [Fact]
  public void VerificationAndPurchaseFixturesRoundTrip()
  {
    string[] verifications =
    [
      "native.memberships.verification-status.pending.default",
      "native.memberships.verification-status.verified.default",
      "native.memberships.verification-status.conflict.default",
      "native.memberships.verification-status.rejected.default",
      "native.memberships.verification.pending.default",
    ];
    foreach (var name in verifications)
    {
      var response = MembershipFixture.Decode<NativeMembershipVerificationResponse>(name);
      Assert.False(string.IsNullOrEmpty(response.Verification.Id));
      MembershipFixture.AssertRoundTrip<NativeMembershipVerificationResponse>(name);
    }

    foreach (var name in new[]
    {
      "native.memberships.purchase-intent.apple.default",
      "native.memberships.purchase-intent.google.default",
      "native.memberships.purchase-intent.microsoft.default",
      "native.memberships.purchase-intent.stripe.default",
    })
    {
      MembershipFixture.AssertRoundTrip<NativeMembershipPurchaseIntentResponse>(name);
    }

    MembershipFixture.AssertRoundTrip<NativeMembershipPurchaseConflict>(
        "native.memberships.purchase-intent.conflict.default");

    Assert.False(MembershipVerificationPolling.IsTerminal("pending", "purchase_pending"));
    Assert.True(MembershipVerificationPolling.IsTerminal("rejected", "wrong_account"));
  }

  [Fact]
  public void CatalogFailsClosedForUnpairedAndUnpricedStoreRows()
  {
    var products = MembershipFixture.Decode<MembershipPlansResponse>("native.memberships.plans.default").Products;
    var empty = MembershipFixture.Decode<NativeMembershipOverview>("native.memberships.me.default");
    Assert.Equal(
        ["voucha.plus.monthly"],
        NativeMembershipCatalog.Rows(products, NativeMembershipStore.AppleIos).Select(row => row.StoreProductId));
    Assert.Equal(
        ["Voucha"],
        NativeMembershipCatalog.Rows(products, NativeMembershipStore.MicrosoftStore).Select(row => row.ApplicationId));
    Assert.Empty(NativeMembershipCatalog.Rows(products, NativeMembershipStore.AppleMacOs));
    Assert.Empty(NativeMembershipCatalog.Rows(products, NativeMembershipStore.MacCatalyst));

    var apple = MembershipFixture.Decode<NativeMembershipPurchaseIntentResponse>(
        "native.memberships.purchase-intent.apple.default");
    Assert.Null(Refusal(apple, NativeMembershipStore.AppleIos, products, empty));
    Assert.Equal(
        MembershipPurchaseRefusal.NoStoreRow,
        Refusal(apple, NativeMembershipStore.AppleMacOs, products, empty));

    var google = MembershipFixture.Decode<NativeMembershipPurchaseIntentResponse>(
        "native.memberships.purchase-intent.google.default");
    Assert.Equal(
        MembershipPurchaseRefusal.NoStoreRow,
        Refusal(google, NativeMembershipStore.GooglePlay, products, empty));
    Assert.Equal(
        MembershipPurchaseRefusal.PlayLaunchMismatch,
        NativeMembershipCatalog.Refusal(
            google.PurchaseIntent with { ProductId = "00000000-0000-7000-8000-000000000702" },
            products,
            NativeMembershipStore.GooglePlay,
            empty));

    var microsoft = MembershipFixture.Decode<NativeMembershipPurchaseIntentResponse>(
        "native.memberships.purchase-intent.microsoft.default");
    Assert.Equal(
        MembershipPurchaseRefusal.NoStoreRow,
        Refusal(microsoft, NativeMembershipStore.MicrosoftStore, products, empty));
    var stripe = MembershipFixture.Decode<NativeMembershipPurchaseIntentResponse>(
        "native.memberships.purchase-intent.stripe.default");
    Assert.Equal(
        MembershipPurchaseRefusal.StripeCheckout,
        Refusal(stripe, NativeMembershipStore.AppleIos, products, empty));
    var lifecycle = MembershipFixture.Decode<NativeMembershipOverview>("native.memberships.me.lifecycle.default");
    Assert.Equal(
        MembershipPurchaseRefusal.ReplacementUnavailable,
        Refusal(apple, NativeMembershipStore.AppleIos, products, lifecycle));
  }

  private static MembershipPurchaseRefusal? Refusal(
      NativeMembershipPurchaseIntentResponse response,
      NativeMembershipStore surface,
      IReadOnlyList<MembershipCatalogProduct> products,
      NativeMembershipOverview overview) =>
      NativeMembershipCatalog.Refusal(response.PurchaseIntent, products, surface, overview);
}
