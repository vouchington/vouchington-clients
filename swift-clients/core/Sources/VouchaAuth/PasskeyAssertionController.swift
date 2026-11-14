// swiftformat:disable indent
#if canImport(AuthenticationServices)
import AuthenticationServices
import Foundation
import VouchaCore

struct PasskeyAuthenticationOptionsResponse: Decodable {
    let options: PasskeyAuthenticationOptions
}

struct PasskeyAuthenticationOptions: Decodable {
    let challenge: String
    let rpId: String
    let userVerification: String?
    let allowCredentials: [PasskeyCredentialDescriptor]?
}

struct PasskeyCredentialDescriptor: Decodable {
    let id: String
}

struct PasskeyAuthenticationResponse: Encodable {
    let id: String
    let rawId: String
    let response: AssertionResponse

    struct AssertionResponse: Encodable {
        let authenticatorData: String
        let clientDataJSON: String
        let signature: String
        let userHandle: String
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(PasskeyAuthenticationJSONValue.object([
            "id": .string(id),
            "rawId": .string(rawId),
            "response": .object([
                "authenticatorData": .string(response.authenticatorData),
                "clientDataJSON": .string(response.clientDataJSON),
                "signature": .string(response.signature),
                "userHandle": .string(response.userHandle)
            ]),
            "type": .string("public-key"),
            "clientExtensionResults": .object([:])
        ]))
    }
}

extension PasskeyAuthenticationResponse {
    init(assertion: any ASAuthorizationPublicKeyCredentialAssertion) {
        let credentialID = assertion.credentialID.base64URLEncodedString()
        let userID: Data? = assertion.userID
        id = credentialID
        rawId = credentialID
        response = .init(
            authenticatorData: assertion.rawAuthenticatorData.base64URLEncodedString(),
            clientDataJSON: assertion.rawClientDataJSON.base64URLEncodedString(),
            signature: assertion.signature.base64URLEncodedString(),
            userHandle: (userID ?? Data()).base64URLEncodedString()
        )
    }
}

private enum PasskeyAuthenticationJSONValue: Encodable {
    case string(String)
    case object([String: PasskeyAuthenticationJSONValue])

    func encode(to encoder: any Encoder) throws {
        switch self {
        case let .string(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case let .object(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        }
    }
}

@MainActor
final class PasskeyAssertionController: NSObject, ASAuthorizationControllerDelegate,
    ASAuthorizationControllerPresentationContextProviding {
    private var continuation: CheckedContinuation<PasskeyAuthenticationResponse, Error>?
    private var controller: ASAuthorizationController?
    private let presentationAnchor: ASPresentationAnchor

    init(presentationAnchor: ASPresentationAnchor) {
        self.presentationAnchor = presentationAnchor
    }

    func assertion(for options: PasskeyAuthenticationOptions) async throws -> PasskeyAuthenticationResponse {
        guard continuation == nil else {
            throw VouchaError.api(statusCode: 0, preconditionCode: "PASSKEY_ASSERTION_IN_PROGRESS")
        }
        let requests = try options.authorizationRequests()

        return try await withCheckedThrowingContinuation { continuation in
            self.continuation = continuation
            let controller = ASAuthorizationController(authorizationRequests: requests)
            controller.delegate = self
            controller.presentationContextProvider = self
            self.controller = controller
            controller.performRequests()
        }
    }

    nonisolated func presentationAnchor(for _: ASAuthorizationController) -> ASPresentationAnchor {
        presentationAnchor
    }

    nonisolated func authorizationController(
        controller _: ASAuthorizationController,
        didCompleteWithAuthorization authorization: ASAuthorization
    ) {
        Task { @MainActor in
            guard let credential = authorization.credential as? any ASAuthorizationPublicKeyCredentialAssertion
            else {
                finish(throwing: VouchaError.api(statusCode: 0, preconditionCode: "INVALID_PASSKEY_ASSERTION"))
                return
            }
            finish(returning: PasskeyAuthenticationResponse(assertion: credential))
        }
    }

    nonisolated func authorizationController(
        controller _: ASAuthorizationController,
        didCompleteWithError error: any Error
    ) {
        Task { @MainActor in
            finish(throwing: error)
        }
    }

    private func finish(returning response: PasskeyAuthenticationResponse) {
        continuation?.resume(returning: response)
        continuation = nil
        clearController()
    }

    private func finish(throwing error: any Error) {
        continuation?.resume(throwing: error)
        continuation = nil
        clearController()
    }

    private func clearController() {
        guard let activeController = controller else { return }
        activeController.delegate = nil
        activeController.presentationContextProvider = nil
        controller = nil
    }
}

#endif
// swiftformat:enable indent
