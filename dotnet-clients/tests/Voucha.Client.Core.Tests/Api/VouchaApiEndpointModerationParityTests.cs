using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  [Fact]
  public void DisputeLifecycleEndpointsUseCanonicalRoutesAndBodies()
  {
    AssertRequest(VouchaApiEndpoints.Dispute("dispute/1"), HttpMethod.Get, "/api/v1/disputes/dispute%2F1");
    AssertRequest(
        VouchaApiEndpoints.UpdateDispute(
            "dispute-1",
            "We reviewed your dispute.",
            "Policy reviewed."),
        HttpMethod.Patch,
        "/api/v1/disputes/dispute-1",
        """{"public_response":"We reviewed your dispute.","internal_notes":"Policy reviewed."}""");
    AssertRequest(
        VouchaApiEndpoints.DisputeApproval("dispute-1"),
        HttpMethod.Post,
        "/api/v1/disputes/dispute-1/approval");
    AssertRequest(
        VouchaApiEndpoints.DisputeDelivery("dispute-1"),
        HttpMethod.Post,
        "/api/v1/disputes/dispute-1/delivery");
    AssertRequest(
        VouchaApiEndpoints.DisputeResolutionDrafts("dispute-1"),
        HttpMethod.Post,
        "/api/v1/disputes/dispute-1/resolution-drafts");
    AssertRequest(
        VouchaApiEndpoints.DisputeResolution("dispute-1", ModerationDisputeResolutionAction.Remove),
        HttpMethod.Post,
        "/api/v1/disputes/dispute-1/resolution",
        """{"action":"remove"}""");
    AssertRequest(
        VouchaApiEndpoints.DisputeResolution(
            "dispute-1",
            ModerationDisputeResolutionAction.Annotate,
            "Visible annotation"),
        HttpMethod.Post,
        "/api/v1/disputes/dispute-1/resolution",
        """{"action":"annotate","body_text":"Visible annotation"}""");
  }

  [Fact]
  public void ExposureEndpointsUseCamelCaseRevealIdentifiers()
  {
    AssertRequest(
        VouchaApiEndpoints.ModerationExposure(),
        HttpMethod.Get,
        "/api/v1/moderation/exposure");
    AssertRequest(
        VouchaApiEndpoints.RecordModerationReveal(
            postId: "post-1",
            reportId: null,
            surface: ModerationRevealSurface.ReviewQueue),
        HttpMethod.Post,
        "/api/v1/moderation/reveals",
        """{"postId":"post-1","surface":"review_queue"}""");
  }

  [Fact]
  public void DisputeDraftAndAnnotationBodiesEnforceTheirInvariants()
  {
    Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.UpdateDispute("dispute-1"));
    Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.DisputeResolution(
        "dispute-1",
        ModerationDisputeResolutionAction.Annotate));
    Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.DisputeResolution(
        "dispute-1",
        ModerationDisputeResolutionAction.Dismiss,
        "Not valid for dismiss"));
  }

  private static void AssertRequest(
      ApiRequest request,
      HttpMethod method,
      string path,
      string? body = null)
  {
    Assert.Equal(method, request.Method);
    Assert.Equal(path, request.Path);
    Assert.Equal(
        body is null ? null : JsonNode.Parse(body),
        request.Body is null
            ? null
            : JsonSerializer.SerializeToNode(request.Body, VouchaApiJson.Options),
        JsonNode.DeepEquals);
  }
}
