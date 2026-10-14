using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatConversationMemberSessionTests
{
  [Fact]
  public async Task MemberChangeResetsCoreConversationAndHiddenReattachCatchesUp()
  {
    var session = new MutableSessionStore("member-a");
    var viewModel = new ChatConversationViewModel(new FakeChatService());
    var queued = new Queue<Action>();
    using var memberSession = new ChatConversationMemberSession(session, viewModel, queued.Enqueue);
    var changes = 0;
    memberSession.MemberChanged += (_, _) => changes++;
    memberSession.Attach();
    await viewModel.LoadAsync("conversation-a", "A", TestContext.Current.CancellationToken);

    session.SetCurrent("member-b");
    Assert.Equal("conversation-a", viewModel.ConversationId);
    Assert.Equal(0, changes);
    Assert.Single(queued).Invoke();
    queued.Clear();
    Assert.Null(viewModel.ConversationId);
    Assert.Equal(1, changes);

    await viewModel.LoadAsync("conversation-b", "B", TestContext.Current.CancellationToken);
    session.SetCurrent("member-c");
    memberSession.Detach();
    Assert.Single(queued).Invoke();
    queued.Clear();
    Assert.Equal("conversation-b", viewModel.ConversationId);
    memberSession.Attach();
    Assert.Null(viewModel.ConversationId);
    Assert.Equal(2, changes);
  }

  private sealed class MutableSessionStore(string memberId) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = Snapshot(memberId);

    public void SetCurrent(string nextMemberId)
    {
      Current = Snapshot(nextMemberId);
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static SessionSnapshot Snapshot(string id) => new(new User(id, id));
  }
}
