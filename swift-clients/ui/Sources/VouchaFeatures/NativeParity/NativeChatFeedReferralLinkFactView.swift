import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeChatFeedReferralLinkFactView: View {
    let links: [NativeChatFeedReferralLinkFact]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftLandingPagesReferralLinks, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            if links.isEmpty {
                Text(UiMessages.string(.nativeSwiftEmptyStateNoReferralLinks, locale: locale))
            } else {
                ForEach(links) { link in
                    referralLink(link)
                }
            }
        }
        .accessibilityIdentifier("native-chat-feed-referral-link-fact")
    }

    private func referralLink(_ link: NativeChatFeedReferralLinkFact) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftReferralLinksManagementReferralProgram, locale: locale))
            Text(verbatim: UiMessages.string(.userContent(link.programName), locale: locale))
            Text(UiMessages.string(.nativeSwiftReferralLinksManagementReferralUrl, locale: locale))
            Text(verbatim: UiMessages.string(.protocolValue(link.url), locale: locale))
        }
        .accessibilityElement(children: .combine)
    }
}
