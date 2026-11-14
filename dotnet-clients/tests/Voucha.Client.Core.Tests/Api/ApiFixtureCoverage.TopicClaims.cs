using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  public static Dictionary<string, Type> WithTopicClaimFixtures(this Dictionary<string, Type> registry)
  {
    registry["native.my-topic-claims.page"] = typeof(TopicClaimPage);
    registry["native.topic-claims.page"] = typeof(TopicClaimPage);
    registry["native.moderation.topic-claims.pending"] = typeof(PendingTopicClaimsResponse);
    registry["native.moderation.topic-claims.verification"] = typeof(TopicClaimMutationResponse);
    registry["native.moderation.topic-claims.rejection"] = typeof(TopicClaimMutationResponse);
    registry["native.moderation.topic-claims.revocation"] = typeof(TopicClaimMutationResponse);
    registry["native.moderation.mod-notes.default"] = typeof(UserModerationNotesResponse);
    registry["native.moderation.mod-notes.create.default"] = typeof(UserModerationNoteResponse);
    return registry;
  }

  private static Dictionary<string, ApiRequest> WithTopicClaimEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    registry["native.my-topic-claims.page"] = VouchaApiEndpoints.MyTopicClaims(limit: 1);
    registry["native.topic-claims.page"] = VouchaApiEndpoints.TopicClaims(
        "00000000-0000-7000-8000-000000000301",
        mineOnly: true,
        activeOnly: true,
        limit: 1);
    const string claimId = "00000000-0000-7000-8000-000000000401";
    const string userId = "00000000-0000-7000-8000-000000000911";
    registry["native.moderation.topic-claims.pending"] = VouchaApiEndpoints.PendingTopicClaims();
    registry["native.moderation.topic-claims.verification"] = VouchaApiEndpoints.VerifyTopicClaim(claimId);
    registry["native.moderation.topic-claims.rejection"] = VouchaApiEndpoints.RejectTopicClaim(claimId, "Insufficient evidence");
    registry["native.moderation.topic-claims.revocation"] = VouchaApiEndpoints.RevokeTopicClaim(claimId, "Evidence withdrawn");
    registry["native.moderation.mod-notes.default"] = VouchaApiEndpoints.UserModerationNotes(userId);
    registry["native.moderation.mod-notes.create.default"] = VouchaApiEndpoints.CreateUserModerationNote(userId,
        new("Observed repeated spam.", "00000000-0000-7000-8000-000000000914"));
    return registry;
  }
}
