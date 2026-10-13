import Foundation
import VouchaAPI

@MainActor
enum NativeChatMCPPersistedGrantCleanup {
    enum Failure: Error { case accountClearingUnavailable }

    struct Input {
        let accountId: String
        let siteOrigin: URL
        let app: MemberMCPApp
        let discovery: MemberMCPOAuthDiscovery
        let tokenStore: any MemberMCPOAuthTokenStore
        let protocolClasses: [AnyClass]?
    }

    static func signOut(_ input: Input) async throws {
        do {
            let metadata = try await input.discovery.discover()
            let tokenClient = try MemberMCPOAuthTokenClient(
                metadata: metadata,
                clientId: input.siteOrigin.appendingPathComponent("api/v1/oauth/native-clients/\(input.app.rawValue)"),
                resource: input.siteOrigin.appendingPathComponent("api/v1/mcp"),
                protocolClasses: input.protocolClasses
            )
            try await MemberMCPTokenManager(
                accountId: input.accountId, store: input.tokenStore, tokenClient: tokenClient
            ).signOut()
        } catch { /* Remote discovery and revocation are best effort; local account deletion is mandatory. */ }
        guard let accountClearing = input.tokenStore as? any MemberMCPOAuthAccountClearing else {
            throw Failure.accountClearingUnavailable
        }
        try await accountClearing.clearAccount(input.accountId)
    }
}
