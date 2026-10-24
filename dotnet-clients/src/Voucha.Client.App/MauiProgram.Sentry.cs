using System.Globalization;
using System.Reflection;
using Sentry;
using Sentry.Maui;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static MauiApp FinishSentry(MauiAppBuilder builder)
  {
    var plan = SentryLaunchPlan.Resolve(
        EnvironmentValues(),
        PackagedSentryConfiguration(),
        releaseConfiguration: !IsDebugBuild(),
        ReleaseName(),
        accountId: null);
    if (plan.StartsSdk)
    {
      builder.UseSentry(options => ApplySentryOptions(options, plan));
    }

    var app = builder.Build();
    if (plan.StartsSdk)
    {
      SentryAccountSync.Attach(app.Services.GetRequiredService<ISessionStore>());
    }

    return app;
  }

  private static void ApplySentryOptions(SentryMauiOptions options, SentryLaunchPlan plan)
  {
    options.Dsn = plan.Dsn;
    options.SendDefaultPii = plan.SendDefaultPii;
    options.Debug = false;
    options.Environment = plan.EnvironmentName;
    options.Release = plan.ReleaseName;
    options.TracesSampleRate = plan.TracesSampleRate;
    options.IncludeTextInBreadcrumbs = false;
    options.IncludeTitleInBreadcrumbs = false;
    options.AttachScreenshot = false;
    options.SetBeforeSend(SentryEventProjection.Scrub);
    options.SetBeforeBreadcrumb(ScrubBreadcrumb);
    options.SetBeforeSendTransaction(SentryTransactionProjection.Scrub);
  }

  private static Breadcrumb? ScrubBreadcrumb(Breadcrumb breadcrumb)
  {
    var scrubbed = SentryEventScrubber.ScrubBreadcrumb(new SentryBreadcrumbDraft(
        breadcrumb.Category,
        breadcrumb.Message,
        breadcrumb.Data));
    return scrubbed is null
        ? null
        : new Breadcrumb(scrubbed.Message ?? "", breadcrumb.Type ?? "default", scrubbed.Data, scrubbed.Category, breadcrumb.Level);
  }

  private static Dictionary<string, string?> EnvironmentValues()
  {
    var values = new Dictionary<string, string?>(StringComparer.Ordinal);
    foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
    {
      var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
      if (key is not null)
      {
        values[key] = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
      }
    }

    return values;
  }

  private static Dictionary<string, string?> PackagedSentryConfiguration() =>
      typeof(MauiProgram).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
          .Where(attribute => attribute.Key == "VOUCHA_SENTRY_DSN")
          .ToDictionary(attribute => attribute.Key, attribute => (string?)attribute.Value, StringComparer.Ordinal);

  private static string ReleaseName()
  {
    var assembly = typeof(MauiProgram).Assembly;
    var display = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";
    var build = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0";
    return display + "+" + build;
  }

  private static bool IsDebugBuild()
  {
#if DEBUG
    return true;
#else
    return false;
#endif
  }
}
