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
        let dsn = read(environment, "VOUCHA_SENTRY_DSN") ?? read(packagedConfiguration, "VOUCHA_SENTRY_DSN")
        return SentryLaunchPlan(
            dsn: dsn,
            releaseName: releaseName,
            environmentName: configuration.environmentName,
            accountId: normalized(accountId),
            sendDefaultPii: false,
            tracesSampleRate: dsn == nil ? 0 : enabledTracesSampleRate
        )
    }

    private static func read(_ source: [String: String], _ key: String) -> String? {
        guard let value = source[key]?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }
        return value
    }

    private static func normalized(_ accountId: String?) -> String? {
        guard let accountId = accountId?.trimmingCharacters(in: .whitespacesAndNewlines), !accountId.isEmpty else {
            return nil
        }
        return accountId
    }
}
