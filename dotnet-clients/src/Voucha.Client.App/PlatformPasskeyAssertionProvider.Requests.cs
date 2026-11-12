#if MACCATALYST
using AuthenticationServices;
using Foundation;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed partial class PlatformPasskeyAssertionProvider
{
  private static ASAuthorizationRequest[] CreateRequests(PasskeyAuthenticationOptions options) =>
      PasskeyAssertionRequestPlan.Create(options).Select(CreateRequest).ToArray();

  private static ASAuthorizationRequest CreateRequest(PasskeyAssertionRequestPlan plan)
  {
    var preference = plan.UserVerification switch
    {
      "required" => ASAuthorizationPublicKeyCredentialUserVerificationPreference.Required,
      "discouraged" => ASAuthorizationPublicKeyCredentialUserVerificationPreference.Discouraged,
      _ => ASAuthorizationPublicKeyCredentialUserVerificationPreference.Preferred,
    };
    using var challenge = NSData.FromArray(plan.Challenge.ToArray());
    if (plan.Kind == PasskeyAuthenticatorKind.Platform)
    {
      using var provider = new ASAuthorizationPlatformPublicKeyCredentialProvider(plan.RpId);
      var request = provider.CreateCredentialAssertionRequest(challenge);
      request.UserVerificationPreference = preference;
      request.AllowedCredentials = plan.AllowedCredentialIds.Select(id =>
          new ASAuthorizationPlatformPublicKeyCredentialDescriptor(NSData.FromArray(id))).ToArray();
      return request;
    }

    using var securityKeyProvider = new ASAuthorizationSecurityKeyPublicKeyCredentialProvider(plan.RpId);
    var securityKeyRequest = securityKeyProvider.Create(challenge);
    securityKeyRequest.UserVerificationPreference = preference;
    securityKeyRequest.AllowedCredentials = plan.AllowedCredentialIds.Select(id =>
        new ASAuthorizationSecurityKeyPublicKeyCredentialDescriptor(NSData.FromArray(id), [])).ToArray();
    return securityKeyRequest;
  }
}
#endif
