/// Identifies one Turnstile-gated endpoint for App Attest assertion binding. The raw value is a
/// cross-repo contract with the backend's `verifyCaptchaOrAttestation(opts.actionTag)` call
/// sites — string literals must match exactly on both sides.
public struct AppAttestActionTag: RawRepresentable, Hashable, Sendable {
    public let rawValue: String

    public init(rawValue: String) {
        self.rawValue = rawValue
    }

    public static let postsCreate = AppAttestActionTag(rawValue: "posts.create")
    public static let communitiesCreate = AppAttestActionTag(rawValue: "communities.create")
    public static let communitiesCreatePost = AppAttestActionTag(rawValue: "communities.create-post")
    public static let disputesCreate = AppAttestActionTag(rawValue: "disputes.create")
    public static let appealsCreate = AppAttestActionTag(rawValue: "appeals.create")
    public static let authEmailAddressTokens = AppAttestActionTag(rawValue: "auth.email-address-tokens")
    public static let topicRecommendationsCreate = AppAttestActionTag(rawValue: "topic-recommendations.create")
    public static let reportsCreate = AppAttestActionTag(rawValue: "reports.create")
}
