using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.HnDiscussions;

public interface IHnDiscussionsSettings
{
  bool Enabled { get; }
}

public sealed class MemoryHnDiscussionsSettings(bool enabled = false) : IHnDiscussionsSettings
{
  public bool Enabled { get; } = enabled;
}

public sealed class SessionHnDiscussionsSettings(ISessionStore sessionStore) : IHnDiscussionsSettings
{
  public bool Enabled => sessionStore.Current.Identity?.HnDiscussions == true;
}
