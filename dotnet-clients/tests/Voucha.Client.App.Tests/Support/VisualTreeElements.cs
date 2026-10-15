using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Voucha.Client.App.Tests;

internal static class VisualTreeElements
{
  public static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }
}
