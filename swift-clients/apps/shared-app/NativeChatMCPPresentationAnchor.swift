import VouchaCore
import VouchaFeatures
#if canImport(AuthenticationServices) && !targetEnvironment(macCatalyst)
    import AuthenticationServices
    import VouchaAPI
    import VouchaAuth
    #if os(macOS)
        import AppKit
    #elseif os(iOS)
        import UIKit
    #endif

    @MainActor
    enum NativeChatMCPPresentationAnchor {
        enum Failure: Error { case unavailable }
    }

#endif

extension ViewModelFactory {
    static func makeNativeChatMCPSession(
        config: AppConfig, accessGroup: String?
    ) -> NativeChatMCPAgentSession? {
        #if canImport(AuthenticationServices) && !targetEnvironment(macCatalyst)
            guard #available(iOS 17.4, macOS 14.4, *) else { return nil }
            guard NativeChatMCPAppleOAuthOrigin.allows(config.baseURL) else { return nil }
            #if os(iOS)
                let app: MemberMCPApp = .ios
            #else
                let app: MemberMCPApp = .macos
            #endif
            guard let service = try? NativeChatMCPNetworkService(
                siteOrigin: config.baseURL,
                app: app,
                tokenStore: KeychainMemberMCPOAuthTokenStore(accessGroup: accessGroup),
                browserCode: { request, window in
                    guard let anchor = window as? ASPresentationAnchor else {
                        throw NativeChatMCPPresentationAnchor.Failure.unavailable
                    }
                    return try await MemberMCPAppleAuthorizationSession().code(
                        for: request, presentationAnchor: anchor
                    )
                }
            ) else { return nil }
            return NativeChatMCPAgentSession(service: service)
        #else
            return nil
        #endif
    }
}
