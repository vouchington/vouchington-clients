import SwiftUI
import VouchaLocalization
import VouchaModels

extension NativeCopyrightNoticesSurface {
    enum DateStyle { case date, timestamp }

    func localized(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        UiMessages.string(key, parameters: parameters, locale: nativeUiLocale)
    }

    func localized(_ key: UiMessageKey, numberParameters: [String: Double]) -> String {
        UiMessages.string(UiMessage(key, numberParameters: numberParameters), locale: nativeUiLocale)
    }

    func localizedDate(_ date: Date, style: DateStyle) -> String {
        UiMessages.date(
            date,
            date: .abbreviated,
            time: style == .timestamp ? .shortened : .omitted,
            locale: nativeUiLocale,
            timeZone: .current
        )
    }

    func surfaceLabel(_ surface: String) -> String {
        Self.surfaceKeys[surface].map { localized($0) }
            ?? surface.replacingOccurrences(of: "-", with: " ")
    }

    @ViewBuilder
    func hostedUseLink(_ value: String) -> some View {
        if let url = URL(string: value), ["http", "https"].contains(url.scheme?.lowercased()) {
            Link(value, destination: url)
        } else {
            Text(value).textSelection(.enabled)
        }
    }

    func timelineMessage(_ event: String) -> UiVerbatimText {
        Self.timelineKeys[event].map { .message($0) }
            ?? .protocolValue(event.replacingOccurrences(of: "_", with: " "))
    }

    func statementDeliveryLabel(_ statement: CopyrightNoticeStatement) -> String {
        let sent = statement.sentAt.map { localizedDate($0, style: .timestamp) }
        return Self.deliveryKeys[statement.state].map { deliveryStatus($0, sent: sent) }
            ?? sent.map { localized(.nativeCopyrightNoticesSentDate, parameters: ["date": $0]) }
            ?? localized(.nativeCopyrightNoticesDeliveryPending)
    }

    func deliveryStatus(_ status: UiMessageKey, sent: String?) -> String {
        guard let sent else { return localized(status) }
        return localized(status) + " · " + localized(.nativeCopyrightNoticesSentDate, parameters: ["date": sent])
    }

    func pathComponent(_ value: String) -> String {
        value.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed
            .subtracting(CharacterSet(charactersIn: "/?#"))) ?? value
    }

    private static let surfaceKeys: [String: UiMessageKey] = [
        "post-image": .nativeCopyrightNoticesSurfacePostImage,
        "user-profile-image": .nativeCopyrightNoticesSurfaceProfileImage,
        "user-profile-link-image": .nativeCopyrightNoticesSurfaceProfileLinkImage,
        "topic-logo-image": .nativeCopyrightNoticesSurfaceTopicLogo,
        "topic-hero-image": .nativeCopyrightNoticesSurfaceTopicHero,
        "community-profile-image": .nativeCopyrightNoticesSurfaceCommunityProfileImage,
        "community-banner-image": .nativeCopyrightNoticesSurfaceCommunityBannerImage
    ]

    private static let timelineKeys: [String: UiMessageKey] = [
        "notice_received": .nativeCopyrightNoticesEventNoticeReceived,
        "provisional_restriction_imposed": .nativeCopyrightNoticesEventProvisionalRestriction,
        "placement_withheld": .nativeCopyrightNoticesEventMaterialWithheld,
        "placement_restored": .nativeCopyrightNoticesEventMaterialRestored,
        "appeal_received": .nativeCopyrightNoticesEventAppealReceived,
        "appeal_reviewed": .nativeCopyrightNoticesEventAppealReviewed,
        "counter_notice_received": .nativeCopyrightNoticesEventCounterNoticeReceived,
        "counter_notice_reviewed": .nativeCopyrightNoticesEventCounterNoticeReviewed,
        "withdrawal_received": .nativeCopyrightNoticesEventWithdrawalReceived
    ]

    private static let deliveryKeys: [String: UiMessageKey] = [
        "failed": .nativeCopyrightNoticesDeliveryFailed,
        "bounced": .nativeCopyrightNoticesDeliveryCouldNotComplete,
        "pending": .nativeCopyrightNoticesDeliveryPending,
        "claimed": .nativeCopyrightNoticesDeliveryPending
    ]
}
