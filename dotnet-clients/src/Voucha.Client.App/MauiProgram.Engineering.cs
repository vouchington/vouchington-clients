using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.App.Pages;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddEngineeringServices(IServiceCollection services)
  {
    services.AddSingleton<IFeatureFlagOverrideStore>(_ =>
        new FileFeatureFlagOverrideStore(
            Path.Combine(FileSystem.AppDataDirectory, "feature-flag-overrides.json")));
    services.AddSingleton<FeatureFlagState>();
    services.AddTransient<IFeatureFlagService, ApiFeatureFlagService>();
    services.AddTransient<FeatureFlagOverridesViewModel>();
    services.AddTransient<IDynamicConfigService, ApiDynamicConfigService>();
    services.AddTransient<DynamicConfigViewModel>();
    services.AddSingleton<IEngineeringService, ApiEngineeringService>();
    services.AddSingleton<IAgentConversationsService, ApiAgentConversationsService>();
    services.AddTransient<EngineeringQueuesViewModel>();
    services.AddTransient<EngineeringPostgreSqlViewModel>();
    services.AddTransient<EngineeringValkeyViewModel>();
    services.AddTransient<AiCostsViewModel>();
    services.AddTransient<AgentConversationViewModel>();
    services.AddTransient<AgentListsViewModel>();
    services.AddTransient<EngineeringPage>();
    services.AddTransient<EngineeringQueuesPage>();
    services.AddTransient<EngineeringPostgreSqlPage>();
    services.AddTransient<EngineeringValkeyPage>();
    services.AddTransient<AiCostsPage>();
    services.AddTransient<DynamicConfigPage>();
    services.AddTransient<FeatureFlagOverridesPage>();
    services.AddTransient<AgentConversationPage>();
    services.AddTransient<AgentDetailPage>();
    services.AddTransient<AgentListsPage>();
  }
}
