import Foundation
import VouchaAPI
import VouchaCore
import VouchaFeatures

extension ViewModelFactory {
    /// Member chat connects to MCP with a token minted from the native session. The browser
    /// authorization anchor is gone; the Keychain store only clears grants left by earlier builds.
    static func makeNativeChatMCPSession(
        config: AppConfig,
        apiClient: APIClient,
        accessGroup: String?,
        currentAccountId: @escaping @MainActor () -> String?
    ) -> NativeChatMCPAgentSession? {
        #if targetEnvironment(macCatalyst)
            return nil
        #else
            #if os(iOS)
                let app: MemberMCPApp = .ios
            #else
                let app: MemberMCPApp = .macos
            #endif
            let siteOrigin = config.baseURL
            let tokenStore = KeychainMemberMCPOAuthTokenStore(accessGroup: accessGroup)
            let service = NativeChatMCPSessionBoundService(
                siteOrigin: siteOrigin,
                app: app,
                apiClient: apiClient,
                currentAccountId: currentAccountId,
                legacyGrantCleanup: NativeChatMCPSessionBoundService.legacyGrantCleanup(
                    siteOrigin: siteOrigin, app: app, tokenStore: tokenStore
                )
            )
            return NativeChatMCPAgentSession(service: service)
        #endif
    }
}
