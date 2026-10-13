using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeCallbackQueryTests
{
  [Fact]
  public void ParseKeepsTheFirstDecodedValueAndDropsKeysWithoutValues()
  {
    var values = NativeCallbackQuery.Parse(
        "?flow_id=expected&flow_id=later&completion_token=a%2Fb+c&bare&empty=");

    Assert.Equal("expected", values["flow_id"]);
    Assert.Equal("a/b+c", values["completion_token"]);
    Assert.Equal(string.Empty, values["empty"]);
    Assert.False(values.ContainsKey("bare"));
    Assert.Equal(3, values.Count);
  }

  [Fact]
  public void ParseTreatsAnEmptyQueryAsNoValues()
  {
    Assert.Empty(NativeCallbackQuery.Parse(string.Empty));
    Assert.Empty(NativeCallbackQuery.Parse("?"));
  }
}
