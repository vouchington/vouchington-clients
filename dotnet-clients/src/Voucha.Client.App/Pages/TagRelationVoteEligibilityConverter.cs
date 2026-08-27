using System.Globalization;
using Microsoft.Maui.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public sealed class TagRelationVoteEligibilityConverter(
    ISessionStore sessionStore,
    Func<bool> isUserTag,
    ElectionVoteChoice? targetChoice) : IMultiValueConverter
{
  public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
  {
    ElectionVoteChoice? currentVote = values.Length > 0 && values[0] is ElectionVoteChoice vote ? vote : null;
    var isMutating = values.Length > 1 && values[1] is true;
    if (isMutating) return false;
    return targetChoice is null
        ? sessionStore.Current.CanClearPublicVote(currentVote)
        : sessionStore.Current.CanCreateEntityRelationVote(isUserTag());
  }

  public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
