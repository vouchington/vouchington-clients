import Foundation
import Sentry
import VouchaCore

enum SentryAppSupport {
    private static var didStart = false
    private static var boundAccountId: String?

    static func startIfConfigured() {
        guard !didStart else { return }
        let plan = SentryLaunchPlan.resolve(
            environment: ProcessInfo.processInfo.environment,
            packagedConfiguration: packagedConfiguration(),
            configuration: buildConfiguration,
            releaseName: releaseName(),
            accountId: nil
        )
        guard plan.startsSDK, let dsn = plan.dsn else { return }
        SentrySDK.start { options in
            options.dsn = dsn
            options.sendDefaultPii = plan.sendDefaultPii
            options.environment = plan.environmentName
            options.releaseName = plan.releaseName
            options.tracesSampleRate = NSNumber(value: plan.tracesSampleRate)
            options.debug = false
            options.beforeSend = { event in
                apply(SentryEventScrubber.scrub(draft(from: event)), to: event)
                return event
            }
            options.beforeBreadcrumb = { breadcrumb in
                scrubbedBreadcrumb(breadcrumb)
            }
        }
        didStart = true
    }

    static func applyAccountId(_ accountId: String?) {
        guard didStart else { return }
        switch SentryAccountBinding.update(from: boundAccountId, to: accountId) {
        case .unchanged:
            return
        case .clear:
            SentrySDK.setUser(nil)
            boundAccountId = nil
        case let .set(accountId):
            SentrySDK.setUser(User(userId: accountId))
            boundAccountId = accountId
        }
    }

    private static var buildConfiguration: SentryBuildConfiguration {
        #if DEBUG
            .debug
        #else
            .release
        #endif
    }

    private static func releaseName() -> String {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0"
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "0"
        return "\(version)+\(build)"
    }

    private static func packagedConfiguration() -> [String: String] {
        guard let info = Bundle.main.infoDictionary else { return [:] }
        var packaged: [String: String] = [:]
        for (key, value) in info {
            if let text = value as? String {
                packaged[key] = text
            }
        }
        return packaged
    }

    private static func draft(from event: Event) -> SentryOutboundEvent {
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
            accountId: event.user?.userId,
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

    private static func apply(_ scrubbed: SentryOutboundEvent, to event: Event) {
        if let accountId = scrubbed.accountId {
            let user = event.user ?? User(userId: accountId)
            user.userId = accountId
            user.email = nil
            user.username = nil
            user.ipAddress = nil
            event.user = user
        } else {
            event.user = nil
        }
        if event.request != nil || scrubbed.requestURL != nil || scrubbed.cookies != nil {
            let request = event.request ?? SentryRequest()
            request.url = scrubbed.requestURL
            request.queryString = nil
            request.headers = scrubbed.headers
            request.cookies = scrubbed.cookies
            event.request = request
        }
        var extra = event.extra ?? [:]
        for key in Array(extra.keys) where drops(key, from: extra, kept: scrubbed.extras) {
            extra.removeValue(forKey: key)
        }
        for (key, value) in scrubbed.extras {
            extra[key] = value
        }
        event.extra = extra
        event.breadcrumbs = (event.breadcrumbs ?? []).compactMap { scrubbedBreadcrumb($0) }
    }

    private static func drops(_ key: String, from extra: [String: Any], kept: [String: String]) -> Bool {
        SentryEventScrubber.dropsExtra(key) || (kept[key] == nil && extra[key] is String)
    }

    private static func scrubbedBreadcrumb(_ breadcrumb: Breadcrumb) -> Breadcrumb? {
        var data: [String: String] = [:]
        for (key, value) in breadcrumb.data ?? [:] {
            if let text = value as? String {
                data[key] = text
            }
        }
        guard let draft = SentryEventScrubber.scrub(
            SentryOutboundEvent(breadcrumbs: [
                SentryBreadcrumbDraft(category: breadcrumb.category, message: breadcrumb.message, data: data)
            ])
        ).breadcrumbs.first else { return nil }
        breadcrumb.category = draft.category
        breadcrumb.message = draft.message
        breadcrumb.data = draft.data
        return breadcrumb
    }
}
