namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private readonly CommunitySectionInitialRouteState initialRouteState = new();

  public string? InitialModerationTransparencyRange
  {
    get => initialRouteState.ModerationTransparencyRange;
    set => initialRouteState.ModerationTransparencyRange = value;
  }
}

internal sealed class CommunitySectionInitialRouteState
{
  private bool hasAppliedModerationTransparencyRange;
  private string? moderationTransparencyRange;

  public string? ModerationTransparencyRange
  {
    get => moderationTransparencyRange;
    set
    {
      moderationTransparencyRange = value;
      hasAppliedModerationTransparencyRange = false;
    }
  }

  public bool TryTakeModerationTransparencyRange(out string? range)
  {
    range = null;
    if (hasAppliedModerationTransparencyRange) return false;
    hasAppliedModerationTransparencyRange = true;
    range = moderationTransparencyRange;
    return true;
  }
}
