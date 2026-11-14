using Voucha.Client.Core.SourceCreation;

namespace Voucha.Client.App.Pages;

public partial class SourceCreationPage : ContentPage, IDisposable
{
  private readonly SourceCreationViewModel viewModel;
  private readonly Func<string, Task> openSource;
  private bool hadParent;

  public SourceCreationPage(SourceCreationViewModel viewModel, Func<string, Task> openSource)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.openSource = openSource;
    BindingContext = viewModel;
  }

  public Task CreateAsync() => viewModel.CreateAsync();
  public Task RetryAsync() => viewModel.RetryAsync();
  public Task OpenAsync() => viewModel.CanOpen ? openSource(viewModel.TopicSlug!) : Task.CompletedTask;
  private async void OnCreateClicked(object? sender, EventArgs args) => await CreateAsync();
  private async void OnRetryClicked(object? sender, EventArgs args) => await RetryAsync();
  private async void OnOpenClicked(object? sender, EventArgs args) => await OpenAsync();

  protected override void OnDisappearing()
  {
    viewModel.Cancel();
    base.OnDisappearing();
  }

  protected override void OnParentSet()
  {
    base.OnParentSet();
    if (Parent is not null) hadParent = true;
    else if (hadParent) Dispose();
  }

  public void Dispose()
  {
    viewModel.Dispose();
    BindingContext = null;
    GC.SuppressFinalize(this);
  }
}
