import Foundation
import VouchaModels

private struct CreateApiKeyBody: Encodable {
    let label: String
    let type: ApiKeyType
    let permissions: [String]
}

private struct CheckoutSessionBody: Encodable {
    let priceId: String
    let successUrl: String
    let cancelUrl: String

    private enum CodingKeys: String, CodingKey {
        case priceId = "price_id"
        case successUrl = "success_url"
        case cancelUrl = "cancel_url"
    }
}

private struct PortalSessionBody: Encodable {
    let returnUrl: String

    private enum CodingKeys: String, CodingKey {
        case returnUrl = "return_url"
    }
}

private struct CreatePushSubscriptionBody: Encodable {
    let endpoint: String
    let p256dh: String
    let auth: String
    let expirationTimeMs: Int?
    let userAgent: String?

    private enum CodingKeys: String, CodingKey {
        case endpoint
        case p256dh
        case auth
        case expirationTimeMs = "expiration_time_ms"
        case userAgent = "user_agent"
    }
}

private struct BeginNativeBlueskyLinkBody: Encodable {
    let handle: String
    let callbackMode = "native"
    let completionProofChallenge: String
}

private struct CompleteNativeBlueskyLinkBody: Encodable {
    let flowId: String
    let completionToken: String
    let completionProofVerifier: String
}

public extension Endpoint {
    static func authSessions(after: String? = nil, limit: Int = 25) -> Endpoint {
        settingsPage(path: "/api/v1/auth/sessions", after: after, limit: limit)
    }

    static func revokeAuthSession(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/auth/sessions/\(pathSegment(id))")
    }

    static var revokeAuthSessions: Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/sessions/revocations")
    }

    static func beginBlueskyAccountLink(handle: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/bluesky/link", body: ["handle": handle])
    }

    static func beginNativeBlueskyAccountLink(handle: String, completionProofChallenge: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/bluesky/link",
            body: BeginNativeBlueskyLinkBody(
                handle: handle,
                completionProofChallenge: completionProofChallenge
            )
        )
    }

    static func completeNativeBlueskyAccountLink(
        flowId: String,
        completionToken: String,
        completionProofVerifier: String
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/bluesky/link-completions",
            body: CompleteNativeBlueskyLinkBody(
                flowId: flowId,
                completionToken: completionToken,
                completionProofVerifier: completionProofVerifier
            )
        )
    }

    static var disconnectBlueskyAccount: Endpoint {
        Endpoint(.DELETE, path: "/api/v1/auth/bluesky/link")
    }

    static func myApiKeys(after: String? = nil, limit: Int = 25) -> Endpoint {
        settingsPage(path: "/api/v1/my/api-keys", after: after, limit: limit)
    }

    static func createMyApiKey(label: String, type: ApiKeyType = .rss, permissions: [String]) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/api-keys",
            body: CreateApiKeyBody(label: label, type: type, permissions: permissions)
        )
    }

    static func revokeMyApiKey(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/api-keys/\(pathSegment(id))")
    }

    static func rotateMyApiKey(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/api-keys/\(pathSegment(id))/rotate")
    }

    static var membershipMe: Endpoint {
        Endpoint(.GET, path: "/api/v1/memberships/me")
    }

    static func membershipCheckout(priceId: String, successUrl: String, cancelUrl: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/memberships/checkout",
            body: CheckoutSessionBody(priceId: priceId, successUrl: successUrl, cancelUrl: cancelUrl)
        )
    }

    static func membershipPortal(returnUrl: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/memberships/billing-portal-sessions",
            body: PortalSessionBody(returnUrl: returnUrl)
        )
    }

    static var membershipCancel: Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/membership")
    }

    static func myPushSubscriptions(after: String? = nil, limit: Int = 25) -> Endpoint {
        settingsPage(path: "/api/v1/my/notifications/push-subscriptions", after: after, limit: limit)
    }

    static func createMyPushSubscription(
        endpoint: String,
        p256dh: String,
        auth: String,
        expirationTimeMs: Int? = nil,
        userAgent: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/notifications/push-subscriptions",
            body: CreatePushSubscriptionBody(
                endpoint: endpoint,
                p256dh: p256dh,
                auth: auth,
                expirationTimeMs: expirationTimeMs,
                userAgent: userAgent
            )
        )
    }

    static func revokeMyPushSubscription(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/notifications/push-subscriptions/\(pathSegment(id))")
    }
}

private func settingsPage(path: String, after: String?, limit: Int) -> Endpoint {
    var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
    if let after {
        queryItems.append(URLQueryItem(name: "after", value: after))
    }
    return Endpoint(.GET, path: path, queryItems: queryItems)
}
