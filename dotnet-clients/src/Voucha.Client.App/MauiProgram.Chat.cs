using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Chat;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddChatServices(IServiceCollection services)
  {
#if WINDOWS
    services.AddSingleton<IMemberMcpProtectedValues, MauiMemberMcpProtectedValues>();
    services.AddSingleton<IMemberMcpAccountTokenStore, ProtectedMemberMcpTokenStore>();
    services.AddSingleton<MemberMcpHttpHandlerFactory>();
    services.AddSingleton<IMemberMcpSessionConnectionFactory>(sp =>
        new MemberMcpSessionBoundConnectionFactory(
            sp.GetRequiredService<AppConfig>().ApiBaseUrl,
            sp.GetRequiredService<VouchaApiClient>(),
            sp.GetRequiredService<ISessionStore>(),
            new MemberMcpSessionConnectionFactory(
                sp.GetRequiredService<AppConfig>().ApiBaseUrl,
                sp.GetRequiredService<IMemberMcpAccountTokenStore>(),
                new MemberMcpRevocationOnlyBrowser(),
                sp.GetRequiredService<MemberMcpHttpHandlerFactory>().Create),
            sp.GetRequiredService<MemberMcpHttpHandlerFactory>().Create));
    services.AddSingleton<MemberMcpSession>();
    services.AddSingleton<MemberMcpAutoConnector>();
#endif
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
        services.GetRequiredService<IUiLocalization>()
#if WINDOWS
        , services.GetRequiredService<MemberMcpSession>()
#endif
        ));
    services.AddSingleton<IChatService, ApiChatService>();
    services.AddTransient<ChatListViewModel>();
    services.AddTransient<ChatConversationViewModel>();
    services.AddTransient<ChatListPage>();
    services.AddTransient<ChatConversationPage>();
  }
}
