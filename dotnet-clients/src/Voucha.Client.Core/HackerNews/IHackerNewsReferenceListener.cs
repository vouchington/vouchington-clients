namespace Voucha.Client.Core.HackerNews;

/// <summary>Receives Hacker News reference flag changes through a weak gate subscription.</summary>
public interface IHackerNewsReferenceListener
{
  void OnHackerNewsReferencesChanged();
}
