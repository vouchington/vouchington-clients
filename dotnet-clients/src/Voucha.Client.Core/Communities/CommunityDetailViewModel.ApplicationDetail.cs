namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  internal event Action? ApplicationDetailContextInvalidated;
  public CommunityApplicationDetailViewModel CreateApplicationDetail(string applicationId)
  {
    var contextRevision = Volatile.Read(ref communityContextRevision);
    var sessionRevision = Volatile.Read(ref agentPromptSessionRevision);
    var detail = new CommunityApplicationDetailViewModel(service, communityIdOrSlug, applicationId);
    detail.AttachOwner(this, () => !agentPromptDisposed && CanModerateCommunity &&
        contextRevision == Volatile.Read(ref communityContextRevision) &&
        sessionRevision == Volatile.Read(ref agentPromptSessionRevision) && IsCurrentAgentPromptSession);
    return detail;
  }
}
