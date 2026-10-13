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
        guard let accountClearing = input.tokenStore as? any MemberMCPOAuthAccountClearing else {
            throw Failure.accountClearingUnavailable
        }
        let grants = try await accountClearing.takeAccount(input.accountId)
        NativeChatMCPRemoteGrantCleanup.start(.init(
            grants: grants, accountId: input.accountId, siteOrigin: input.siteOrigin,
            app: input.app, discovery: input.discovery, protocolClasses: input.protocolClasses,
            knownMetadata: nil, activeClient: nil
        ))
    }
}
