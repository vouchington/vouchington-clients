using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Notifications;

namespace Voucha.Client.App.Pages;

public partial class NotificationsPage : ContentPage
{
  private readonly NotificationsViewModel viewModel;
  private readonly AppShell appShell;
  private readonly ILogger<NotificationsPage> logger;
  private bool isNavigating;

  public NotificationsPage(
      NotificationsViewModel viewModel,
      AppShell appShell,
      ILogger<NotificationsPage> logger)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.appShell = appShell;
    this.logger = logger;
    BindingContext = new NotificationsPageBinding(viewModel, () => viewModel.ReloadAsync());
    PaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow load failures to escape to the dispatcher.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow load failures to escape to the dispatcher.")]
  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await viewModel.LoadNextPageAsync().ConfigureAwait(true);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow mutation failures to escape to the dispatcher.")]
  private async void OnMarkReadClicked(object? sender, EventArgs e)
  {
    try
    {
      if (sender is Button { CommandParameter: NotificationRow item })
      {
        await viewModel.MarkReadAsync(item.Id);
      }
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow mutation failures to escape to the dispatcher.")]
  private async void OnNotificationTapped(object? sender, TappedEventArgs e)
  {
    if (isNavigating) return;
    isNavigating = true;
    try
    {
      var item = e.Parameter as NotificationRow ??
          (sender as Border)?.BindingContext as NotificationRow;
      if (item is not null)
      {
        var targetPath = await viewModel.ActivateAsync(item);
        if (NotificationTargetPolicy.TryResolve(targetPath, out var target))
        {
          if (target.IsExternalWebLink)
          {
            await Launcher.Default.OpenAsync(target.Uri);
            return;
          }

          await AppLinkDispatcher.DispatchAsync(target.Uri);
        }
      }
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
    finally
    {
      isNavigating = false;
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow mutation failures to escape to the dispatcher.")]
  private async void OnMarkAllReadClicked(object? sender, EventArgs e)
  {
    try
    {
      await viewModel.MarkAllReadAsync();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void event handlers must not allow navigation failures to escape to the dispatcher.")]
  private async void OnNotificationSettingsClicked(object? sender, EventArgs e)
  {
    try
    {
      await NotificationSettingsInboxAction.OpenAsync(appShell);
    }
    catch (Exception ex)
    {
      LogNotificationSettingsNavigationFailed(logger, ex);
    }
  }

  [LoggerMessage(
      EventId = 1,
      Level = LogLevel.Error,
      Message = "Notification settings navigation failed.")]
  private static partial void LogNotificationSettingsNavigationFailed(
      ILogger logger,
      Exception exception);
}

public sealed class NotificationsPageBinding : LoadablePageBinding<NotificationRow>
{
  private readonly NotificationsViewModel viewModel;

  public NotificationsPageBinding(NotificationsViewModel viewModel, Func<Task> refresh) : base(refresh)
  {
    this.viewModel = viewModel;
    // Only surface the view-model changes this page binds to.
    viewModel.PropertyChanged += (_, e) =>
    {
      if (string.IsNullOrEmpty(e.PropertyName) ||
          e.PropertyName == nameof(NotificationsViewModel.Items) ||
          e.PropertyName == nameof(NotificationsViewModel.HasError) ||
          e.PropertyName == nameof(NotificationsViewModel.ErrorMessage) ||
          e.PropertyName == nameof(NotificationsViewModel.IsLoading) ||
          e.PropertyName == nameof(NotificationsViewModel.HasMore))
      {
        NotifyLoadStateChanged();
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(PaginationIsLoading));
      }

      if (string.IsNullOrEmpty(e.PropertyName) ||
          e.PropertyName == nameof(NotificationsViewModel.CanMarkAllRead))
      {
        OnPropertyChanged(nameof(CanMarkAllRead));
      }
    };
  }

  public override IReadOnlyList<NotificationRow> Items => viewModel.Items;

  public override bool HasError => viewModel.HasError;

  public override string? ErrorMessage => viewModel.ErrorMessage;

  public bool CanMarkAllRead => viewModel.CanMarkAllRead;

  public bool HasMore => viewModel.HasMore;

  public bool PaginationIsLoading => viewModel.IsLoading;

  protected override bool IsLoading => viewModel.IsLoading;
}
