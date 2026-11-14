import Foundation

public enum SentryBuildConfiguration: Equatable, Sendable {
    case debug
    case release

    public var environmentName: String {
        switch self {
        case .debug:
            "debug"
        case .release:
            "release"
        }
    }
}

/// Whether a build should start Sentry. A missing DSN disables it for Debug and Release.
public struct SentryLaunchPlan: Equatable, Sendable {
    public static let enabledTracesSampleRate = 0.1

    public let dsn: String?
    public let releaseName: String
    public let environmentName: String
    public let accountId: String?
    public let sendDefaultPii: Bool
    public let tracesSampleRate: Double

    public var startsSDK: Bool {
        dsn != nil
    }

    public static func resolve(
        environment: [String: String] = [:],
        packagedConfiguration: [String: String] = [:],
        configuration: SentryBuildConfiguration,
        releaseName: String,
        accountId: String?
    ) -> SentryLaunchPlan {
        let dsn = validDSN(
            SentryTrimmedText.nonEmpty(environment["VOUCHA_SENTRY_DSN"])
                ?? SentryTrimmedText.nonEmpty(packagedConfiguration["VOUCHA_SENTRY_DSN"])
        )
        return SentryLaunchPlan(
            dsn: dsn,
            releaseName: configuredText("VOUCHA_SENTRY_RELEASE", environment, packagedConfiguration) ?? releaseName,
            environmentName: configuredText("VOUCHA_SENTRY_ENVIRONMENT", environment, packagedConfiguration)
                ?? configuration.environmentName,
            accountId: SentryTrimmedText.nonEmpty(accountId),
            sendDefaultPii: false,
            tracesSampleRate: dsn == nil ? 0 : enabledTracesSampleRate
        )
    }

    private static func configuredText(
        _ key: String,
        _ environment: [String: String],
        _ packagedConfiguration: [String: String]
    ) -> String? {
        SentryTrimmedText.nonEmpty(environment[key])
            ?? SentryTrimmedText.nonEmpty(packagedConfiguration[key])
    }

    private static func validDSN(_ value: String?) -> String? {
        guard let value, let components = URLComponents(string: value),
              let host = components.host, !host.isEmpty,
              let user = components.user, !user.isEmpty,
              let project = components.path.split(separator: "/").last, !project.isEmpty
        else { return nil }
        return value
    }
}
