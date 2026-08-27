using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public sealed record DynamicConfigNamespaceOption(
    DynamicConfigNamespaceSummary Summary)
{
  public string ExternalContentLabel => Summary.Label;

  public string ProtocolNamespace => Summary.Namespace;

  public string ExternalContentDescription => Summary.Description;
}
