using Voucha.Client.Core.SourceCreation;
using Xunit;

namespace Voucha.Client.Core.Tests.SourceCreation;

public sealed class SourceCreationTopicResolutionTests
{
  [Theory]
  [InlineData("row-topic")]
  [InlineData("source;topic")]
  [InlineData("réseau")]
  public async Task CompletedRowTopicAvoidsUnavailableDetailEndpoint(string rowTopic)
  {
    var completed = SourceCreationTests.Status("completed");
    var service = new SourceCreationTests.FakeService
    {
      DefaultStatus = completed with { Rows = [completed.Rows[0] with { TopicSlug = rowTopic }] },
      Resolve = _ => throw new HttpRequestException(),
    };
    using var model = SourceCreationTests.Model(service);
    model.Input = "https://example.test/feed.xml";

    await model.CreateAsync();

    Assert.True(model.CanOpen);
    Assert.Equal(rowTopic, model.TopicSlug);
    Assert.Null(model.Error);
    Assert.Equal(0, service.Resolves);
    Assert.Equal(1, service.Posts);
    Assert.Equal(1, service.Gets);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" \t")]
  [InlineData("bad/slug")]
  [InlineData("bad\\slug")]
  [InlineData("bad?query")]
  [InlineData("bad#fragment")]
  [InlineData("bad%2Fslug")]
  [InlineData(".")]
  [InlineData("..")]
  [InlineData("bad slug")]
  [InlineData("\u0001")]
  [InlineData("bad\u200Bslug")]
  public async Task InvalidOrMissingRowTopicUsesResolvedTopic(string? rowTopic)
  {
    var completed = SourceCreationTests.Status("completed");
    var service = new SourceCreationTests.FakeService
    {
      DefaultStatus = completed with { Rows = [completed.Rows[0] with { TopicSlug = rowTopic }] },
    };
    using var model = SourceCreationTests.Model(service);
    model.Input = "https://example.test/feed.xml";

    await model.CreateAsync();

    Assert.True(model.CanOpen);
    Assert.Equal("resolved-topic", model.TopicSlug);
    Assert.Null(model.Error);
    Assert.Equal(1, service.Resolves);
    Assert.Equal(1, service.Posts);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" \t")]
  [InlineData("bad/slug")]
  [InlineData("bad\\slug")]
  [InlineData("bad?query")]
  [InlineData("bad#fragment")]
  [InlineData("bad%2Fslug")]
  [InlineData(".")]
  [InlineData("..")]
  [InlineData("bad slug")]
  [InlineData("\u0001")]
  [InlineData("bad\u200Bslug")]
  public async Task InvalidOrMissingDetailTopicRemainsGetRetryable(string? detailTopic)
  {
    var service = new SourceCreationTests.FakeService
    {
      Resolve = id => Task.FromResult(new CreatedSourceDetail(id, detailTopic)),
    };
    using var model = SourceCreationTests.Model(service);
    model.Input = "https://example.test/feed.xml";

    await model.CreateAsync();

    Assert.False(model.CanOpen);
    Assert.True(model.CanRetry);
    Assert.False(model.CanCreate);
    Assert.NotNull(model.Error);
    service.Resolve = id => Task.FromResult(new CreatedSourceDetail(id, "resolved-after-retry"));
    await model.RetryAsync();

    Assert.True(model.CanOpen);
    Assert.Equal("resolved-after-retry", model.TopicSlug);
    Assert.Null(model.Error);
    Assert.Equal(1, service.Posts);
    Assert.Equal(2, service.Gets);
    Assert.Equal(2, service.Resolves);
  }
}
