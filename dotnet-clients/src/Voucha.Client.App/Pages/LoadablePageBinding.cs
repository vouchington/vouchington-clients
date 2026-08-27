using System.Windows.Input;

namespace Voucha.Client.App.Pages;

public abstract class LoadablePageBinding<TItem> : BindableObject
{
  private readonly Func<Task> refresh;
  private bool isRefreshing;

  protected LoadablePageBinding(Func<Task> refresh)
  {
    this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
    RefreshCommand = new Command(() => _ = RefreshAsync());
  }

  public abstract IReadOnlyList<TItem> Items { get; }

  public abstract bool HasError { get; }

  public abstract string? ErrorMessage { get; }

  protected abstract bool IsLoading { get; }

  public bool IsRefreshing
  {
    get => isRefreshing;
    private set
    {
      if (isRefreshing == value) return;
      isRefreshing = value;
      OnPropertyChanged();
    }
  }

  public ICommand RefreshCommand { get; }

  protected void NotifyLoadStateChanged()
  {
    OnPropertyChanged(nameof(Items));
    OnPropertyChanged(nameof(HasError));
    OnPropertyChanged(nameof(ErrorMessage));
  }

  private async Task RefreshAsync()
  {
    if (IsRefreshing || IsLoading) return;
    IsRefreshing = true;
    try
    {
      await refresh().ConfigureAwait(true);
    }
    finally
    {
      IsRefreshing = false;
    }
  }
}
