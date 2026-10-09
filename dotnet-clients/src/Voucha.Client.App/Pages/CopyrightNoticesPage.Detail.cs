using Voucha.Client.Core.Api;
using Voucha.Client.Core.Copyright;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class CopyrightNoticesPage
{
  private void RenderDetail(CopyrightCaseSnapshot snapshot)
  {
    var notice = snapshot.Notice;
    var isEu = notice.Jurisdiction == "eu_dsa";
    body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesCase,
        ("id", UiText.Verbatim(notice.Id))));
    if (isEu)
    {
      body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesEuDetailTitle));
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesReceivedDate,
          ("date", UiCopy.FormatDateTime(notice.ReceivedAt))));
    }
    else if (snapshot.AcceptedAt is { } accepted)
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesAcceptedDate,
          ("date", UiCopy.FormatDateTime(accepted))));
    if (!isEu) AddClaimant(notice);
    if (!isEu || snapshot.Targets.Count > 0)
    {
      body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesAffectedHostedMaterial));
      foreach (var target in snapshot.Targets) AddTarget(target);
    }
    if (!isEu)
    {
      body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesCaseTimeline));
      foreach (var entry in snapshot.Timeline)
      {
        body.Children.Add(TimelineKey(entry.EventType) is { } key
            ? Message(key) : ProtocolValue(entry.EventType.Replace('_', ' ')));
        body.Children.Add(Value(UiCopy.FormatDateTime(entry.CreatedAt)));
      }
    }
    AddStatements(snapshot.Participant?.Statements);
    if (isEu && snapshot.Participant?.Eu is { } eu) RenderEu(eu);
  }

  private void AddStatements(IReadOnlyList<CopyrightParticipantStatement>? statements)
  {
    if (statements is not { Count: > 0 }) return;
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesStatementsAndDecisions));
    foreach (var statement in statements)
    {
      if (statement.State == "bounced")
        body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesDeliveryCouldNotComplete));
      else if (statement.State == "failed")
        body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesDeliveryFailed));
      else if (statement.SentAt is { } sentAt)
        body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesSentDate, ("date", UiCopy.FormatDateTime(sentAt))));
      else body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesDeliveryPending));
      body.Children.Add(Value(statement.Text));
    }
  }

  private void AddTarget(CopyrightNoticeTarget target)
  {
    body.Children.Add(SurfaceKey(target.Surface) is { } key ? Message(key) : ProtocolValue(target.Surface));
    if (target.HostedUseUrl is { IsAbsoluteUri: true } uri && uri.Scheme is "https" or "http")
    {
      var button = new Button { Text = UiCopy.Resolve(UiText.Verbatim(uri.AbsoluteUri)), AutomationId = $"copyright-target-{target.Id}" };
      button.Clicked += (_, _) => _ = openExternal(uri);
      body.Children.Add(button);
    }
    else body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesHostedMaterialUnavailable));
    body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesStatus,
        ("status", UiText.ProtocolValue(target.RestrictionStatus))));
  }

  private static UiMessageKey? SurfaceKey(string surface) => surface switch
  {
    "post-image" => UiMessageKey.NativeCopyrightNoticesSurfacePostImage,
    "user-profile-image" => UiMessageKey.NativeCopyrightNoticesSurfaceProfileImage,
    "user-profile-link-image" => UiMessageKey.NativeCopyrightNoticesSurfaceProfileLinkImage,
    "topic-logo-image" => UiMessageKey.NativeCopyrightNoticesSurfaceTopicLogo,
    "topic-hero-image" => UiMessageKey.NativeCopyrightNoticesSurfaceTopicHero,
    "community-profile-image" => UiMessageKey.NativeCopyrightNoticesSurfaceCommunityProfileImage,
    "community-banner-image" => UiMessageKey.NativeCopyrightNoticesSurfaceCommunityBannerImage,
    _ => null,
  };

  private static UiMessageKey? TimelineKey(string eventType) => eventType switch
  {
    "notice_received" => UiMessageKey.NativeCopyrightNoticesEventNoticeReceived,
    "provisional_restriction_imposed" => UiMessageKey.NativeCopyrightNoticesEventProvisionalRestriction,
    "placement_withheld" => UiMessageKey.NativeCopyrightNoticesEventMaterialWithheld,
    "placement_restored" => UiMessageKey.NativeCopyrightNoticesEventMaterialRestored,
    "appeal_received" => UiMessageKey.NativeCopyrightNoticesEventAppealReceived,
    "appeal_reviewed" => UiMessageKey.NativeCopyrightNoticesEventAppealReviewed,
    "counter_notice_received" => UiMessageKey.NativeCopyrightNoticesEventCounterNoticeReceived,
    "counter_notice_reviewed" => UiMessageKey.NativeCopyrightNoticesEventCounterNoticeReviewed,
    "withdrawal_received" => UiMessageKey.NativeCopyrightNoticesEventWithdrawalReceived,
    _ => null,
  };

  private static Label ProtocolValue(string text) => new() { Text = UiCopy.Resolve(UiText.ProtocolValue(text)) };
}
