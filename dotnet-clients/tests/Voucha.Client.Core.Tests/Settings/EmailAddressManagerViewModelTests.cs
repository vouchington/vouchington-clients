using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class EmailAddressManagerViewModelTests
{
  [Fact]
  public async Task EmptyAccountRequestsNormalizedAddressAndVerifiesEightCharacterOtp()
  {
    var service = new RecordingEmailAddressService();
    var viewModel = new EmailAddressManagerViewModel(service) { EmailAddress = " User@Example.com " };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("No email addresses yet", viewModel.EmptyStateMessage);

    Assert.True(await viewModel.RequestVerificationAsync(TestContext.Current.CancellationToken));
    Assert.Equal("user@example.com", viewModel.EmailAddress);
    Assert.True(viewModel.IsAwaitingVerification);

    viewModel.Otp = "ABCD1234";
    Assert.True(await viewModel.VerifyAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Email verified. Try your action again.", viewModel.SuccessMessage);
    Assert.Single(viewModel.EmailAddresses);
    Assert.True(viewModel.EmailAddresses[0].IsPrimary);
  }

  [Fact]
  public async Task OtpMustContainExactlyEightCharacters()
  {
    var service = new RecordingEmailAddressService();
    var viewModel = new EmailAddressManagerViewModel(service) { EmailAddress = "user@example.com" };
    await viewModel.RequestVerificationAsync(TestContext.Current.CancellationToken);
    viewModel.Otp = "short";

    Assert.False(await viewModel.VerifyAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Enter the 8-character verification code.", viewModel.ErrorMessage);
    Assert.Equal(0, service.VerifyCalls);
  }

  [Fact]
  public async Task EmailAddressIsRequiredBeforeRequestingVerification()
  {
    var viewModel = new EmailAddressManagerViewModel(new RecordingEmailAddressService());

    Assert.False(await viewModel.RequestVerificationAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Enter an email address.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task EmailAddressContinuationAppendsUniqueRowsAndForwardsTheCursor()
  {
    var first = new EmailAddress("first@example.com", true, DateTimeOffset.Parse("2026-07-11T12:00:00Z"));
    var second = new EmailAddress("second@example.com", false, DateTimeOffset.Parse("2026-07-12T12:00:00Z"));
    var service = new RecordingEmailAddressService
    {
      InitialResponse = new([first], new PageInfo("next", true, null)),
      ContinuationResponse = new([first, second], new PageInfo(null, false, null)),
    };
    var viewModel = new EmailAddressManagerViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreEmailAddressesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["first@example.com", "second@example.com"], viewModel.EmailAddresses.Select(item => item.Address));
    Assert.Equal("next", service.ContinuationAfter);
    Assert.False(viewModel.HasMoreEmailAddresses);
  }

  private sealed class RecordingEmailAddressService : IEmailAddressService
  {
    public int VerifyCalls { get; private set; }
    public EmailAddressListResponse InitialResponse { get; init; } = new([]);
    public EmailAddressListResponse ContinuationResponse { get; init; } = new([]);
    public string? ContinuationAfter { get; private set; }

    public Task<EmailAddressListResponse> FetchEmailAddressesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(InitialResponse);

    public Task<EmailAddressListResponse> FetchEmailAddressesPageAsync(
        string? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
      ContinuationAfter = after;
      return Task.FromResult(after is null ? InitialResponse : ContinuationResponse);
    }

    public Task<EmailAddressRequestResponse> RequestEmailVerificationAsync(
        string emailAddress,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new EmailAddressRequestResponse(emailAddress.Trim().ToLowerInvariant()));

    public Task<EmailAddressListResponse> VerifyEmailAddressAsync(
        string emailAddress,
        string token,
        CancellationToken cancellationToken = default)
    {
      VerifyCalls++;
      return Task.FromResult(new EmailAddressListResponse([
        new EmailAddress(emailAddress, true, DateTimeOffset.Parse("2026-07-11T12:00:00Z")),
      ]));
    }
  }
}
