import Foundation

enum TurnstileBuildConfiguration: Equatable {
    case debug
    case release

    static var current: Self {
        #if DEBUG
            .debug
        #else
            .release
        #endif
    }
}

enum TurnstileSiteKeyPolicy {
    static let alwaysPassSiteKey = "1x00000000000000000000AA"
    static let missingReleaseKeyMessage =
        "Release builds require an explicit non-test VOUCHA_TURNSTILE_SITE_KEY."
    static let rejectedTestKeyMessage =
        "Release builds reject Cloudflare Turnstile test site keys."
    static let testSiteKeys: Set<String> = [
        alwaysPassSiteKey,
        "2x00000000000000000000AB",
        "1x00000000000000000000BB",
        "2x00000000000000000000BB",
        "3x00000000000000000000FF",
        "1x0000000000000000000000000000000AA",
        "2x0000000000000000000000000000000AA",
        "3x0000000000000000000000000000000AA"
    ]

    enum Resolution: Equatable {
        case key(String)
        case rejected(String)
    }

    static func resolve(_ supplied: String, configuration: TurnstileBuildConfiguration) -> Resolution {
        let key = supplied.trimmingCharacters(in: .whitespacesAndNewlines)
        switch configuration {
        case .debug:
            return .key(key.isEmpty ? alwaysPassSiteKey : key)
        case .release:
            if key.isEmpty { return .rejected(missingReleaseKeyMessage) }
            if testSiteKeys.contains(key) { return .rejected(rejectedTestKeyMessage) }
            return .key(key)
        }
    }

    static func required(_ supplied: String) -> String {
        switch resolve(supplied, configuration: .current) {
        case let .key(key):
            key
        case let .rejected(message):
            preconditionFailure(message)
        }
    }
}
