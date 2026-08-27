using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.App.Pages;

public partial class SupportThreadPage : ContentPage
{
  private readonly SupportThreadViewModel viewModel;
  private string? threadId;

  public SupportThreadPage(SupportThreadViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
  }

  public void SetContext(string threadId) => this.threadId = threadId;

  public async Task ApplyRouteAsync(string threadId)
  {
    SetContext(threadId);
    await viewModel.LoadAsync(threadId).ConfigureAwait(true);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      if (threadId is { Length: > 0 })
      {
        await viewModel.LoadAsync(threadId).ConfigureAwait(true);
      }
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnLoadMoreClicked(object? sender, EventArgs e)
  {
    try
    {
      await viewModel.LoadMoreAsync().ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
