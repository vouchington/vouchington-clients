namespace Voucha.Client.Core.Chat;

public interface ILocalLLMConfigurationTransaction
{
  LocalLLMConfiguration Configuration { get; }
  void Save(LocalLLMConfiguration configuration);
  void Clear();
}

internal sealed class LocalLLMConfigurationTransaction(LocalLLMConfiguration configuration, Action<LocalLLMConfiguration> save, Action clear) : ILocalLLMConfigurationTransaction
{
  public LocalLLMConfiguration Configuration { get; private set; } = configuration;

  public void Save(LocalLLMConfiguration next)
  {
    ArgumentNullException.ThrowIfNull(next);
    save(next);
    Configuration = next;
  }

  public void Clear()
  {
    clear();
    Configuration = new();
  }
}
