namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  public bool SupportsFediverseProviders => mode == OmnisearchMode.Fediverse;

  public FediverseProvider SelectedFediverseProvider
  {
    get => selectedFediverseProvider;
    private set
    {
      if (selectedFediverseProvider == value) return;
      selectedFediverseProvider = value;
      OnPropertyChanged();
    }
  }
}
