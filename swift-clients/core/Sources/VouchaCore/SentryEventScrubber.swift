import Foundation

public struct SentryBreadcrumbDraft: Equatable, Sendable {
    public var category: String
    public var message: String?
    public var data: [String: String]

    public init(category: String, message: String? = nil, data: [String: String] = [:]) {
        self.category = category
        self.message = message
        self.data = data
    }
}

public struct SentryOutboundEvent: Equatable, Sendable {
    public var accountId: String?
    public var email: String?
    public var username: String?
    public var ipAddress: String?
    public var requestURL: String?
    public var queryString: String?
    public var headers: [String: String]
    public var cookies: String?
    public var body: String?
    public var extras: [String: String]
    public var breadcrumbs: [SentryBreadcrumbDraft]

    public init(
        accountId: String? = nil,
        email: String? = nil,
        username: String? = nil,
        ipAddress: String? = nil,
        requestURL: String? = nil,
        queryString: String? = nil,
        headers: [String: String] = [:],
        cookies: String? = nil,
        body: String? = nil,
        extras: [String: String] = [:],
        breadcrumbs: [SentryBreadcrumbDraft] = []
    ) {
        self.accountId = accountId
        self.email = email
        self.username = username
        self.ipAddress = ipAddress
        self.requestURL = requestURL
        self.queryString = queryString
        self.headers = headers
        self.cookies = cookies
        self.body = body
        self.extras = extras
        self.breadcrumbs = breadcrumbs
    }
}

public protocol SentryEventTransport {
    func send(_ event: SentryOutboundEvent)
}

public struct SentryRuntime {
    public let plan: SentryLaunchPlan
    private let transport: any SentryEventTransport

    public static func open(
        environment: [String: String] = [:],
        packagedConfiguration: [String: String] = [:],
        configuration: SentryBuildConfiguration,
        releaseName: String,
        accountId: String?,
        transport: any SentryEventTransport
    ) -> SentryRuntime? {
        let plan = SentryLaunchPlan.resolve(
            environment: environment,
            packagedConfiguration: packagedConfiguration,
            configuration: configuration,
            releaseName: releaseName,
            accountId: accountId
        )
        guard plan.startsSDK else { return nil }
        return SentryRuntime(plan: plan, transport: transport)
    }

    public func capture(_ event: SentryOutboundEvent) {
        transport.send(SentryEventScrubber.scrub(event))
    }
}

public enum SentryEventScrubber {
    public static let filteredValue = "[Filtered]"

    public static func dropsExtra(_ key: String) -> Bool {
        droppedExtraKeys.contains(key.lowercased())
    }

    public static func scrub(_ event: SentryOutboundEvent) -> SentryOutboundEvent {
        var scrubbed = event
        scrubbed.email = nil
        scrubbed.username = nil
        scrubbed.ipAddress = nil
        scrubbed.accountId = normalized(event.accountId)
        scrubbed.requestURL = stripQuery(event.requestURL)
        scrubbed.queryString = nil
        scrubbed.headers = scrubHeaders(event.headers)
        scrubbed.cookies = event.cookies?.isEmpty == false ? filteredValue : nil
        scrubbed.body = nil
        scrubbed.extras = scrubExtras(event.extras)
        scrubbed.breadcrumbs = event.breadcrumbs.compactMap(scrubBreadcrumb)
        return scrubbed
    }

    private static func scrubBreadcrumb(_ breadcrumb: SentryBreadcrumbDraft) -> SentryBreadcrumbDraft? {
        let category = breadcrumb.category.lowercased()
        if typedTextCategories.contains(category) || category.contains("text") || category.contains("input") {
            return nil
        }
        if breadcrumb.data.keys.contains(where: { typedTextKeys.contains($0.lowercased()) }) {
            return nil
        }
        var data: [String: String] = [:]
        for (key, value) in breadcrumb.data {
            let name = key.lowercased()
            if droppedExtraKeys.contains(name) || isCredentialHeader(name) || isSeparateRequestPart(name) {
                continue
            }
            data[key] = name.contains("url") ? stripQuery(value) ?? value : value
        }
        return SentryBreadcrumbDraft(category: breadcrumb.category, message: breadcrumb.message, data: data)
    }

    private static func scrubHeaders(_ headers: [String: String]) -> [String: String] {
        var result: [String: String] = [:]
        for (name, value) in headers {
            result[name] = isCredentialHeader(name) ? filteredValue : value
        }
        return result
    }

    private static func scrubExtras(_ extras: [String: String]) -> [String: String] {
        var result: [String: String] = [:]
        for (key, value) in extras where !droppedExtraKeys.contains(key.lowercased()) {
            result[key] = value
        }
        return result
    }

    private static func isSeparateRequestPart(_ name: String) -> Bool {
        name == "http.query" || name == "http.fragment" || name == "query" || name == "fragment"
    }

    private static func isCredentialHeader(_ name: String) -> Bool {
        let lowered = name.lowercased()
        return credentialHeaders.contains(lowered) || lowered.hasPrefix("x-app-attest-")
    }

    private static func stripQuery(_ value: String?) -> String? {
        guard let value, !value.isEmpty else { return nil }
        let query = value.firstIndex(of: "?")
        let fragment = value.firstIndex(of: "#")
        let cut: String.Index? = switch (query, fragment) {
        case let (query?, fragment?):
            min(query, fragment)
        case let (query?, nil):
            query
        case let (nil, fragment?):
            fragment
        case (nil, nil):
            nil
        }
        guard let cut else { return value }
        return String(value[..<cut])
    }

    private static func normalized(_ accountId: String?) -> String? {
        guard let accountId = accountId?.trimmingCharacters(in: .whitespacesAndNewlines), !accountId.isEmpty else {
            return nil
        }
        return accountId
    }

    private static let typedTextCategories: Set<String> = ["ui.text", "ui.input", "ui.textfield", "user.input"]
    private static let typedTextKeys: Set<String> = ["text", "input", "typed", "value"]
    private static let droppedExtraKeys: Set<String> = [
        "body", "request_body", "response_body", "http.request.body.data", "chat", "post", "prompt",
        "completion", "email_code", "otp", "token", "access_token", "refresh_token", "authorization",
        "cookie", "password", "secret"
    ]
    private static let credentialHeaders: Set<String> = [
        "authorization", "proxy-authorization", "cookie", "x-api-key", "cf-access-jwt-assertion",
        "x-cf-worker-secret", "x-bedrock-batch-shared-key", "x-voucha-cache-purge-secret",
        "x-app-attest-assertion", "x-app-attest-nonce", "x-app-attest-challenge-id", "stripe-signature",
        "signature", "copyright-guest-capability", "idempotency-key"
    ]
}
