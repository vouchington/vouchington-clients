import Foundation
import Sentry
import VouchaCore

enum SentryAppSupport {
    private static let stateLock = NSLock()
    private static var didStartStorage = false
    private static var boundAccountIdStorage: String?

    private static var didStart: Bool {
        get { stateLock.withLock { didStartStorage } }
        set { stateLock.withLock { didStartStorage = newValue } }
    }

    private static var boundAccountId: String? {
        get { stateLock.withLock { boundAccountIdStorage } }
        set { stateLock.withLock { boundAccountIdStorage = newValue } }
    }

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
                let draft = SentryEventProjection.draft(from: event, boundAccountId: boundAccountId)
                SentryEventProjection.apply(SentryEventScrubber.scrub(draft), to: event)
                return event
            }
            options.beforeBreadcrumb = { breadcrumb in
                SentryEventProjection.scrubbedBreadcrumb(breadcrumb)
            }
            options.beforeSendSpan = { span in
                SentrySpanProjection.scrub(span)
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
            let user = User(userId: accountId)
            user.data = [SentryEventProjection.accountMarker: accountId]
            SentrySDK.setUser(user)
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
}
