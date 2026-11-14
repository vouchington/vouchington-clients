namespace Voucha.Client.Core;

public sealed class SentryRuntime
{
  private readonly ISentryEventTransport transport;

  private SentryRuntime(ISentryEventTransport transport) => this.transport = transport;

  public static SentryRuntime? Open(SentryLaunchPlan plan, ISentryEventTransport transport)
  {
    ArgumentNullException.ThrowIfNull(plan);
    ArgumentNullException.ThrowIfNull(transport);
    return plan.StartsSdk ? new SentryRuntime(transport) : null;
  }

  public void Capture(SentryOutboundEvent outbound)
  {
    ArgumentNullException.ThrowIfNull(outbound);
    transport.Send(SentryEventScrubber.Scrub(outbound));
  }
}
