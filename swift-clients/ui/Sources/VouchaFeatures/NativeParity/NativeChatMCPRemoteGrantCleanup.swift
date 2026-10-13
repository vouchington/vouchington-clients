import Foundation
import VouchaAPI

/// Local Keychain removal has already completed before any of these optional network calls start.
@MainActor
enum NativeChatMCPRemoteGrantCleanup {
    struct Input {
        let grants: [MemberMCPOAuthStoredGrant]
        let accountId: String
        let siteOrigin: URL
        let app: MemberMCPApp
        let discovery: MemberMCPOAuthDiscovery
        let protocolClasses: [AnyClass]?
        let knownMetadata: MemberMCPOAuthMetadata?
        let activeClient: MemberMCPClient?
    }

    static func start(_ input: Input) {
        guard !input.grants.isEmpty else { return }
        _ = Task {
            let metadata: MemberMCPOAuthMetadata
            if let knownMetadata = input.knownMetadata {
                metadata = knownMetadata
            } else {
                guard let discovered = try? await input.discovery.discover() else { return }
                metadata = discovered
            }
            let clientId = input.siteOrigin.appendingPathComponent(
                "api/v1/oauth/native-clients/\(input.app.rawValue)"
            )
            let resource = input.siteOrigin.appendingPathComponent("api/v1/mcp")
            let currentScope = MemberMCPOAuthTokenScope(
                accountId: input.accountId, issuerIdentifier: metadata.issuerIdentifier,
                resource: resource, clientId: clientId
            )
            guard let tokenClient = try? MemberMCPOAuthTokenClient(
                metadata: metadata, clientId: clientId, resource: resource,
                protocolClasses: input.protocolClasses
            ) else { return }
            for grant in input.grants where grant.scope == currentScope {
                if let activeClient = input.activeClient,
                   grant.tokens.expiresIn > 30,
                   grant.tokens.acquiredAt.addingTimeInterval(TimeInterval(grant.tokens.expiresIn - 30)) > Date() {
                    _ = Task { try? await activeClient.close(accessToken: grant.tokens.accessToken) }
                }
                _ = Task { try? await tokenClient.revoke(grant.tokens.refreshToken) }
            }
        }
    }
}
