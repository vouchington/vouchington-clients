using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Chat;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddChatServices(IServiceCollection services)
  {
    services.AddSingleton<IChatProviderResolver, ChatProviderResolver>();
    services.AddSingleton<LocalLLMFeaturePolicy>();
    services.AddSingleton<ILocalLLMConfigurationStore>(_ =>
        new FileLocalLLMConfigurationStore(Path.Combine(FileSystem.AppDataDirectory, "local-llm-settings.json")));
    services.AddSingleton<ILocalLLMSecretStore, MauiLocalLLMSecretStore>();
#if WINDOWS
    services.AddSingleton<IWindowsSystemLanguageModelRuntime, WindowsSystemLanguageModelRuntime>();
#endif
    services.AddSingleton(_ => new OpenAICompatibleResponsesClient(TimeSpan.FromSeconds(600)));
    services.AddSingleton<ILocalChatProvider>(services => new OpenAICompatibleLocalChatProvider(
        services.GetRequiredService<ILocalLLMConfigurationStore>(), services.GetRequiredService<ILocalLLMSecretStore>(),
        services.GetRequiredService<OpenAICompatibleResponsesClient>(), services.GetRequiredService<LocalLLMFeaturePolicy>(), Guid.Empty,
        services.GetRequiredService<IUiLocalization>()));
    services.AddSingleton<IChatService, ApiChatService>();
    services.AddTransient<ChatListViewModel>();
    services.AddTransient<ChatConversationViewModel>();
    services.AddTransient<ChatListPage>();
    services.AddTransient<ChatConversationPage>();
  }
}
