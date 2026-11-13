#if canImport(AuthenticationServices)
    import AuthenticationServices
    import Foundation
    import VouchaCore

    extension PasskeyAuthenticationOptions {
        @MainActor
        func authorizationRequests() throws -> [ASAuthorizationRequest] {
            let challenge = try Data(base64URLEncoded: challenge)
            let credentialIDs = try (allowCredentials ?? []).map { try Data(base64URLEncoded: $0.id) }
            let platform = ASAuthorizationPlatformPublicKeyCredentialProvider(relyingPartyIdentifier: rpId)
                .createCredentialAssertionRequest(challenge: challenge)
            platform.userVerificationPreference = userVerificationPreference
            platform.allowedCredentials = credentialIDs.map {
                ASAuthorizationPlatformPublicKeyCredentialDescriptor(credentialID: $0)
            }
            let securityKey = ASAuthorizationSecurityKeyPublicKeyCredentialProvider(relyingPartyIdentifier: rpId)
                .createCredentialAssertionRequest(challenge: challenge)
            securityKey.userVerificationPreference = userVerificationPreference
            securityKey.allowedCredentials = credentialIDs.map {
                ASAuthorizationSecurityKeyPublicKeyCredentialDescriptor(
                    credentialID: $0,
                    transports: [.usb, .nfc, .bluetooth]
                )
            }
            return [platform, securityKey]
        }

        private var userVerificationPreference: ASAuthorizationPublicKeyCredentialUserVerificationPreference {
            switch userVerification {
            case "required": .required
            case "discouraged": .discouraged
            default: .preferred
            }
        }
    }
#endif
