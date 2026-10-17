using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed record QueueDomainRow(QueueDomain Source, IUiLocalization? Localization = null)
{
  public UiText DomainText => UiText.Verbatim(Source.Domain);
  public UiText ReasonText => UiText.Verbatim(Source.Reason ?? string.Empty);
  public string DomainPresentation => (Localization ?? UiLocalization.English).Resolve(DomainText);
  public string ReasonPresentation => (Localization ?? UiLocalization.English).Resolve(ReasonText);
  public bool Paused => Source.Paused;
  public DomainStateCount Waiting => Source.Waiting;
  public DomainStateCount Active => Source.Active;
  public DomainStateCount Delayed => Source.Delayed;
  public DomainStateCount Completed => Source.Completed;
  public DomainStateCount Failed => Source.Failed;
}
