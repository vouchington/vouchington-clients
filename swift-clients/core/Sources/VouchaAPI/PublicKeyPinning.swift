import Foundation

public struct PublicKeyPin: Equatable, Sendable {
    public let host: String
    public let spkiSha256Base64: String
    public let label: String

    public init(host: String, spkiSha256Base64: String, label: String) {
        precondition(!spkiSha256Base64.isEmpty, "spkiSha256Base64 must not be empty")
        precondition(!label.isEmpty, "label must not be empty")
        self.host = Self.normalizeHost(host)
        self.spkiSha256Base64 = spkiSha256Base64
        self.label = label
    }

    private static func normalizeHost(_ host: String) -> String {
        host.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }
}

public enum PublicKeyPinValidationResult: Equatable, Sendable {
    case accepted
    case rejected
    case notPinned
}

public struct PublicKeyPinningPolicy: Sendable {
    public static let vouchaDefault = PublicKeyPinningPolicy(
        eligibleHosts: ["voucha.ai", "staging.voucha.ai"],
        pins: []
    )

    private let eligibleHosts: Set<String>
    private let pinsByHost: [String: Set<String>]

    public init(eligibleHosts: Set<String>, pins: [PublicKeyPin]) {
        self.eligibleHosts = Set(eligibleHosts.map(Self.normalizeHost))
        pinsByHost = Dictionary(grouping: pins, by: \.host)
            .mapValues { Set($0.map(\.spkiSha256Base64)) }
    }

    /// Indicates whether any host has pins configured; used to audit zero-pin rollout defaults.
    public var hasConfiguredPins: Bool {
        pinsByHost.values.contains { !$0.isEmpty }
    }

    public func isEligibleHost(_ host: String) -> Bool {
        eligibleHosts.contains(Self.normalizeHost(host))
    }

    public func requiresPinning(for url: URL) -> Bool {
        guard url.scheme?.lowercased() == "https", let host = url.host.map(Self.normalizeHost) else {
            return false
        }
        return isEligibleHost(host) && pinsByHost[host]?.isEmpty == false
    }

    public func validate(host: String, spkiSha256Base64: String) -> PublicKeyPinValidationResult {
        let normalizedHost = Self.normalizeHost(host)
        guard isEligibleHost(normalizedHost), let pins = pinsByHost[normalizedHost], !pins.isEmpty else {
            return .notPinned
        }
        return pins.contains(spkiSha256Base64) ? .accepted : .rejected
    }

    private static func normalizeHost(_ host: String) -> String {
        host.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }
}
