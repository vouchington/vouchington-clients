using Microsoft.Maui.Controls.Xaml;

namespace Voucha.Client.App;

[ContentProperty(nameof(Path))]
[AcceptEmptyServiceProvider]
public sealed class UiLocalizedValueExtension : IMarkupExtension<BindingBase>
{
  public string Path { get; set; } = ".";

  public string? Format { get; set; }

  public BindingBase ProvideValue(IServiceProvider serviceProvider)
  {
    var format = Format;
    ArgumentException.ThrowIfNullOrWhiteSpace(format);
    var resources = Application.Current?.Resources
        ?? throw new InvalidOperationException("Application resources are not available.");
    var converter = resources["UiLocalizedValue"] as IMultiValueConverter
        ?? throw new InvalidOperationException("UiLocalizedValue converter is not registered.");
    var version = resources["UiLocaleVersion"] as UiLocaleVersion
        ?? throw new InvalidOperationException("UiLocaleVersion is not registered.");
    return new MultiBinding
    {
      Converter = converter,
      ConverterParameter = format,
      Bindings =
      {
        new Binding(Path),
        new Binding(nameof(UiLocaleVersion.Version), source: version),
      },
    };
  }

  object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
      ProvideValue(serviceProvider);
}
