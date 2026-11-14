using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Voucha.Client.Core.Support;

public abstract class ObservableObject : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler? PropertyChanged;

  protected bool SetProperty<T>(
      ref T field,
      T value,
      [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
  }

  protected bool SetPropertyAndNotify<T>(
      ref T field,
      T value,
      string dependentProperty,
      [CallerMemberName] string? propertyName = null)
  {
    if (!SetProperty(ref field, value, propertyName)) return false;
    OnPropertyChanged(dependentProperty);
    return true;
  }

  protected bool SetPropertyAndNotify<T>(
      ref T field,
      T value,
      ReadOnlySpan<string> dependentProperties,
      [CallerMemberName] string? propertyName = null)
  {
    if (!SetProperty(ref field, value, propertyName)) return false;
    foreach (var dependentProperty in dependentProperties)
      OnPropertyChanged(dependentProperty);
    return true;
  }

  protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
