import Foundation
import Sentry
import VouchaCore

enum SentryEventProjection {
    static let accountMarker = "voucha.account"

    static func draft(from event: Event, boundAccountId: String?) -> SentryOutboundEvent {
        let request = event.request
        var extras: [String: String] = [:]
        for (key, value) in event.extra ?? [:] {
            if let text = value as? String {
                extras[key] = text
            }
        }
        let breadcrumbs = (event.breadcrumbs ?? []).map { breadcrumb in
            var data: [String: String] = [:]
            for (key, value) in breadcrumb.data ?? [:] {
                if let text = value as? String {
                    data[key] = text
                }
            }
            return SentryBreadcrumbDraft(
                category: breadcrumb.category, message: breadcrumb.message, data: data
            )
        }
        return SentryOutboundEvent(
            accountId: capturedAccountId(event) ?? boundAccountMatching(event, boundAccountId: boundAccountId),
            email: event.user?.email,
            username: event.user?.username,
            ipAddress: event.user?.ipAddress,
            requestURL: request?.url,
            queryString: request?.queryString,
            headers: request?.headers ?? [:],
            cookies: request?.cookies,
            body: extras["body"] ?? extras["request_body"] ?? extras["response_body"],
            extras: extras,
            breadcrumbs: breadcrumbs
        )
    }

    static func apply(_ scrubbed: SentryOutboundEvent, to event: Event) {
        if let accountId = scrubbed.accountId {
            let user = User(userId: accountId)
            user.data = [Self.accountMarker: accountId]
            event.user = user
        } else {
            event.user = nil
        }
        event.tags = scrubbedTags(event.tags)
        event.context = scrubbedContext(event.context)
        if event.request != nil || scrubbed.requestURL != nil || scrubbed.cookies != nil {
            let request = event.request ?? SentryRequest()
            request.url = scrubbed.requestURL
            request.queryString = nil
            request.fragment = nil
            request.headers = scrubbed.headers
            request.cookies = scrubbed.cookies
            event.request = request
        }
        var extra = event.extra ?? [:]
        for key in Array(extra.keys) where scrubbed.extras[key] == nil {
            extra.removeValue(forKey: key)
        }
        for (key, value) in scrubbed.extras {
            extra[key] = value
        }
        event.extra = extra
        event.breadcrumbs = (event.breadcrumbs ?? []).compactMap { scrubbedBreadcrumb($0) }
    }

    static func scrubbedBreadcrumb(_ breadcrumb: Breadcrumb) -> Breadcrumb? {
        var strings: [String: String] = [:]
        var numbers: [String: NSNumber] = [:]
        for (key, value) in breadcrumb.data ?? [:] {
            if let text = value as? String {
                strings[key] = text
            } else if keepsNumber(key, value) {
                numbers[key] = value as? NSNumber
            }
        }
        guard let draft = SentryEventScrubber.scrub(
            SentryOutboundEvent(breadcrumbs: [
                SentryBreadcrumbDraft(category: breadcrumb.category, message: breadcrumb.message, data: strings)
            ])
        ).breadcrumbs.first else { return nil }
        var data: [String: Any] = draft.data
        for (key, number) in numbers {
            data[key] = number
        }
        breadcrumb.category = draft.category
        breadcrumb.message = draft.message
        breadcrumb.data = data
        return breadcrumb
    }

    static func keepsNumber(_ key: String, _ value: Any) -> Bool {
        value is NSNumber && SentryEventScrubber.keepsNumericField(key)
    }

    private static func scrubbedTags(_ tags: [String: String]?) -> [String: String]? {
        guard let tags else { return nil }
        var retained: [String: String] = [:]
        for (key, value) in tags {
            if let kept = SentryEventScrubber.scrubField(key: key, value: value) {
                retained[key] = kept
            }
        }
        return retained
    }

    private static func scrubbedContext(_ context: [String: [String: Any]]?) -> [String: [String: Any]]? {
        guard let context else { return nil }
        var retained: [String: [String: Any]] = [:]
        for (name, values) in context {
            var kept: [String: Any] = [:]
            for (key, value) in values {
                if let text = value as? String, let scrubbed = SentryEventScrubber.scrubField(key: key, value: text) {
                    kept[key] = scrubbed
                } else if keepsNumber(key, value) {
                    kept[key] = value
                }
            }
            if !kept.isEmpty {
                retained[name] = kept
            }
        }
        return retained
    }

    private static func capturedAccountId(_ event: Event) -> String? {
        guard let value = event.user?.data?[accountMarker] as? String else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    private static func boundAccountMatching(_ event: Event, boundAccountId: String?) -> String? {
        guard let boundAccountId, event.user?.userId == boundAccountId else { return nil }
        return boundAccountId
    }
}
