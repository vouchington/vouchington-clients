using Voucha.Client.Core.Pagination;
using Xunit;

namespace Voucha.Client.Core.Tests.Pagination;

public sealed class CursorPaginationStateTests
{
  [Fact]
  public void AppendsDistinctItemsAndForwardsOpaqueCursor()
  {
    var state = new CursorPaginationState<Item, string>(item => item.Id);
    var first = Assert.IsType<CursorPageRequest>(state.BeginInitialPageIfNeeded());
    Assert.Null(first.Cursor);
    Assert.True(state.Complete(first, [new("a"), new("b")], "opaque-1", hasNextPage: true));

    var second = Assert.IsType<CursorPageRequest>(state.BeginNextPage());
    Assert.Equal("opaque-1", second.Cursor);
    Assert.True(state.Complete(second, [new("b"), new("c")], null, hasNextPage: false));

    Assert.Equal(["a", "b", "c"], state.Items.Select(item => item.Id));
    Assert.False(state.HasMore);
  }

  [Fact]
  public void PrependsDistinctItemsWithoutBreakingForwardAppendBehavior()
  {
    var state = new CursorPaginationState<Item, string>(item => item.Id, [new("root"), new("near")]);
    state.RestoreContinuation("older", hasMore: true);
    var request = Assert.IsType<CursorPageRequest>(state.BeginNextPage());

    Assert.True(state.CompletePrepending(request, [new("root"), new("far")], null, hasNextPage: false));
    Assert.Equal(["root", "far", "near"], state.Items.Select(item => item.Id));
  }

  [Fact]
  public void FailurePreservesRowsBlocksAutomaticLoadingAndRetriesSameCursor()
  {
    var state = new CursorPaginationState<Item, string>(item => item.Id);
    var first = Assert.IsType<CursorPageRequest>(state.BeginNextPage());
    state.Complete(first, [new("a")], "retry-me", hasNextPage: true);
    var failed = Assert.IsType<CursorPageRequest>(state.BeginNextPage());

    Assert.True(state.Fail(failed, "offline"));
    Assert.Equal(["a"], state.Items.Select(item => item.Id));
    Assert.False(state.CanAutomaticallyLoad);

    var retry = Assert.IsType<CursorPageRequest>(state.BeginNextPage());
    Assert.Equal("retry-me", retry.Cursor);
  }

  [Fact]
  public void ResetRejectsStaleCompletionAndAllowsOnlyOneFlight()
  {
    var state = new CursorPaginationState<Item, string>(item => item.Id);
    var stale = Assert.IsType<CursorPageRequest>(state.BeginNextPage());
    Assert.Null(state.BeginNextPage());
    state.Reset();

    Assert.False(state.Complete(stale, [new("stale")], null, hasNextPage: false));
    Assert.Empty(state.Items);
    Assert.NotNull(state.BeginInitialPageIfNeeded());
  }

  [Fact]
  public void CancellationPreservesLoadedRowsAndAllowsAutomaticContinuation()
  {
    var state = new CursorPaginationState<Item, string>(item => item.Id, [new("a")]);
    state.RestoreContinuation("after-a", hasMore: true);
    var request = Assert.IsType<CursorPageRequest>(state.BeginNextPage());

    Assert.True(state.Cancel(request));

    Assert.Equal(["a"], state.Items.Select(item => item.Id));
    Assert.True(state.CanAutomaticallyLoad);
    Assert.Equal("after-a", Assert.IsType<CursorPageRequest>(state.BeginNextPage()).Cursor);
  }

  [Fact]
  public void ReplacementDropsExistingRowsAndRejectsAStaleGeneration()
  {
    var state = new CursorPaginationState<Item, string>(item => item.Id, [new("paid")]);
    state.RestoreContinuation("older", hasMore: true);
    var current = Assert.IsType<CursorPageRequest>(state.BeginNextPage());

    Assert.True(state.CompleteReplacing(current, [new("locked")], null, hasNextPage: false));
    Assert.Equal(["locked"], state.Items.Select(item => item.Id));
    Assert.False(state.HasMore);

    state.Reset([new("newer")]);
    var stale = Assert.IsType<CursorPageRequest>(state.BeginNextPage());
    state.Reset([new("newest")]);
    Assert.False(state.CompleteReplacing(stale, [new("stale")], null, hasNextPage: false));
    Assert.Equal(["newest"], state.Items.Select(item => item.Id));
  }

  private sealed record Item(string Id);
}
