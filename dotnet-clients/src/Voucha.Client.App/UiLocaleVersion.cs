using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public sealed class UiLocaleVersion : BindableObject, IUiLocaleChangeListener, IDisposable
{
  private readonly IDisposable subscription;
  private int version;

  public UiLocaleVersion(IUiLocaleController localeController) =>
      subscription = localeController.SubscribeLocaleChanges(this);

  public int Version
  {
    get => version;
    private set
    {
      if (version == value) return;
      version = value;
      OnPropertyChanged();
    }
  }

  public void OnUiLocaleChanged() => Version = unchecked(Version + 1);

  public void Dispose() => subscription.Dispose();
}
