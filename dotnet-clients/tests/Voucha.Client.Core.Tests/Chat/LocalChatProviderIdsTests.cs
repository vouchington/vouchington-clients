using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class LocalChatProviderIdsTests
{
  [Fact]
  public void OpenAICompatibleUsesTheUnderscorePrefixAndLowercaseGuid()
  {
    var profileId = Guid.Parse("70cd237f-6920-4a4a-81de-03dad9afbab6");
    Assert.Equal("openai_compatible:70cd237f-6920-4a4a-81de-03dad9afbab6", LocalChatProviderIds.OpenAICompatible(profileId));
  }

  [Theory]
  [InlineData("openai_compatible:70cd237f-6920-4a4a-81de-03dad9afbab6")]
  [InlineData("openai-compatible:70cd237f-6920-4a4a-81de-03dad9afbab6")]
  public void TryGetEndpointIdAcceptsBothSeparators(string providerId)
  {
    var profileId = Guid.Parse("70cd237f-6920-4a4a-81de-03dad9afbab6");
    Assert.Equal(profileId, LocalChatProviderIds.TryGetEndpointId(providerId));
    Assert.True(LocalChatProviderIds.RefersTo(providerId, profileId));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("windows-system-language-model")]
  [InlineData("openai_compatible:not-a-guid")]
  [InlineData("openai-compatible:not-a-guid")]
  [InlineData("openai")]
  public void TryGetEndpointIdRejectsNonEndpointIds(string? providerId)
  {
    Assert.Null(LocalChatProviderIds.TryGetEndpointId(providerId));
    Assert.False(LocalChatProviderIds.RefersTo(providerId, Guid.NewGuid()));
  }

  [Fact]
  public void NormalizeRewritesOnlyTheHyphenPrefix()
  {
    var profileId = Guid.Parse("70cd237f-6920-4a4a-81de-03dad9afbab6");
    var canonical = LocalChatProviderIds.OpenAICompatible(profileId);
    Assert.Equal(canonical, LocalChatProviderIds.Normalize($"openai-compatible:{profileId:D}"));
    Assert.Equal(canonical, LocalChatProviderIds.Normalize(canonical));
    Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel,
        LocalChatProviderIds.Normalize(LocalChatProviderIds.WindowsSystemLanguageModel));
    Assert.Null(LocalChatProviderIds.Normalize(null));
  }
}
