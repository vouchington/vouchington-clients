using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Copyright;

public sealed partial class CopyrightNoticesViewModelTests
{
  [Fact]
  public async Task FailedNextPageRetainsRowsAndRetriesTheExactOpaqueCursor()
  {
    var original = Fixture<CopyrightNoticesResponse>("web.copyright.notices.default");
    var first = original with { PageInfo = new PageInfo("opaque/+==", true, null) };
    var last = original with
    {
      CopyrightNotices = [original.CopyrightNotices[0] with { Id = "next-case" }],
      PageInfo = new PageInfo(null, false, null),
    };
    var attempts = 0;
    var service = new CaseService
    {
      Page = (after, _, _) => after is null ? Task.FromResult(first)
          : ++attempts == 1 ? Task.FromException<CopyrightNoticesResponse>(new HttpRequestException("offline"))
          : Task.FromResult(last),
    };
    var model = Model(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Single(model.Notices);
    Assert.True(model.HasError);
    Assert.True(model.HasMore);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["list::25", "list:opaque/+==:25", "list:opaque/+==:25"], service.Calls);
    Assert.Equal(2, model.Notices.Count);
    Assert.False(model.HasMore);
    Assert.False(model.HasError);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3, service.Calls.Count);
  }

  [Fact]
  public async Task SettlementPagesAppendWithoutDuplicateRowsAndRetainOpaqueCursorOnFailure()
  {
    var original = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.eu.participant.no-action-complaint");
    var eu = original.CopyrightNotice.Eu!;
    var participant = original with
    {
      CopyrightNotice = original.CopyrightNotice with
      {
        Eu = eu with { DisputeSettlementsPageInfo = new PageInfo("case-scoped/+==", true, null) },
      },
    };
    var firstRow = eu.DisputeSettlements[0];
    var page = new CopyrightEuDisputeSettlementsResponse(
        [firstRow, firstRow with { Id = "next-settlement" }], new PageInfo(null, false, null));
    var attempts = 0;
    var service = new CaseService
    {
      Participant = (_, _) => Task.FromResult(participant),
      Settlements = (_, _, _, _) => ++attempts == 1
          ? Task.FromException<CopyrightEuDisputeSettlementsResponse>(new HttpRequestException("offline"))
          : Task.FromResult(page),
    };
    var model = Model(service);
    await model.LoadCaseAsync("eu", TestContext.Current.CancellationToken);
    await model.LoadMoreSettlementsAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasSettlementError);
    Assert.Single(model.SelectedCase!.Participant!.Eu!.DisputeSettlements);
    await model.LoadMoreSettlementsAsync(TestContext.Current.CancellationToken);
    var updated = model.SelectedCase!.Participant!.Eu!;
    Assert.Equal(2, updated.DisputeSettlements.Count);
    Assert.False(updated.DisputeSettlementsPageInfo.HasNextPage);
    Assert.False(model.HasSettlementError);
    Assert.Equal(2, service.Calls.Count(call => call == $"settlements:{original.CopyrightNotice.Id}:case-scoped/+==:25"));
  }

  [Fact]
  public async Task LateSettlementPageCannotOverwriteANewlyOpenedCase()
  {
    var euResponse = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.eu.participant.no-action-complaint");
    euResponse = euResponse with
    {
      CopyrightNotice = euResponse.CopyrightNotice with
      {
        Eu = euResponse.CopyrightNotice.Eu! with { DisputeSettlementsPageInfo = new PageInfo("old-case", true, null) },
      },
    };
    var other = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.notice.participant.populated");
    var page = Fixture<CopyrightEuDisputeSettlementsResponse>("web.copyright.eu.dispute-settlements.participant");
    var held = new TaskCompletionSource<CopyrightEuDisputeSettlementsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new CaseService
    {
      Participant = (id, _) => Task.FromResult(id == "eu" ? euResponse : other),
      Settlements = (_, _, _, _) => held.Task,
    };
    var model = Model(service);
    await model.LoadCaseAsync("eu", TestContext.Current.CancellationToken);
    var pending = model.LoadMoreSettlementsAsync(TestContext.Current.CancellationToken);
    try
    {
      Assert.True(model.IsLoadingSettlements);
      await model.LoadCaseAsync("other", TestContext.Current.CancellationToken);
      held.TrySetResult(page);
      await pending;
      Assert.Null(model.SelectedCase!.Participant!.Eu);
      Assert.Equal(other.CopyrightNotice.Id, model.SelectedCase.Notice.Id);
    }
    finally
    {
      held.TrySetResult(page);
      await pending;
    }
  }
  [Fact]
  public async Task LateSettlementFailureDoesNotAddAnErrorToANewCase()
  {
    var original = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.eu.participant.no-action-complaint");
    var euResponse = original with
    {
      CopyrightNotice = original.CopyrightNotice with
      {
        Eu = original.CopyrightNotice.Eu! with { DisputeSettlementsPageInfo = new PageInfo("old", true, null) },
      },
    };
    var other = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.notice.participant.populated");
    var held = new TaskCompletionSource<CopyrightEuDisputeSettlementsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new CaseService
    {
      Participant = (id, _) => Task.FromResult(id == "eu" ? euResponse : other),
      Settlements = (_, _, _, _) => held.Task,
    };
    var model = Model(service);
    await model.LoadCaseAsync("eu", TestContext.Current.CancellationToken);
    var pending = model.LoadMoreSettlementsAsync(TestContext.Current.CancellationToken);
    try
    {
      await model.LoadCaseAsync("other", TestContext.Current.CancellationToken);
      held.TrySetException(new HttpRequestException("late offline response"));
      await pending;
      Assert.False(model.HasSettlementError);
      Assert.Equal(other.CopyrightNotice.Id, model.SelectedCase!.Notice.Id);
    }
    finally
    {
      held.TrySetException(new HttpRequestException("drain"));
      await pending;
    }
  }

}
