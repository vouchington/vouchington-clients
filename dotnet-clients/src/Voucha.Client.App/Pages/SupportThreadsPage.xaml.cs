using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.App.Pages;

public partial class SupportThreadsPage : ContentPage
{
  private readonly SupportThreadsViewModel viewModel;
  private readonly IServiceProvider serviceProvider;

  public SupportThreadsPage(SupportThreadsViewModel viewModel, IServiceProvider serviceProvider)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    BindingContext = viewModel;
    PaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
  }

  public async Task ApplyRouteAsync(string? conversationId)
  {
    SetInitialConversationId(conversationId);
    await viewModel.LoadAsync().ConfigureAwait(true);
  }

  public void SetInitialConversationId(string? conversationId) =>
      ConversationIdEntry.Text = string.IsNullOrWhiteSpace(conversationId) ? string.Empty : conversationId.Trim();

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handlers must not throw.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await viewModel.LoadNextPageAsync().ConfigureAwait(true);

  private async void OnCreateClicked(object? sender, EventArgs e)
  {
    try
    {
      var row = await viewModel.CreateThreadAsync(
          SubjectEntry.Text ?? string.Empty,
          MessageEditor.Text,
          string.IsNullOrWhiteSpace(ConversationIdEntry.Text) ? null : ConversationIdEntry.Text.Trim())
          .ConfigureAwait(true);
      if (row is null) return;

      SubjectEntry.Text = string.Empty;
      MessageEditor.Text = string.Empty;
      ConversationIdEntry.Text = string.Empty;

      var page = serviceProvider.GetRequiredService<SupportThreadPage>();
      page.SetContext(row.Id);
      await Navigation.PushAsync(page).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnOpenClicked(object? sender, EventArgs e)
  {
    try
    {
      if (sender is Button { CommandParameter: SupportThreadRow row })
      {
        var page = serviceProvider.GetRequiredService<SupportThreadPage>();
        page.SetContext(row.Id);
        await Navigation.PushAsync(page).ConfigureAwait(true);
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
