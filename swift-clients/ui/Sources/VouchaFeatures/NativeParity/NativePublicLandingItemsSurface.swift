import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativePublicLandingItemsSurface: View {
    @Environment(\.openURL)
    var openURL
    @Environment(\.locale)
    var locale
    let page: NativePublicLandingPage
    let viewModel: NativeRouteSurfaceViewModel
    let onNavigate: (String) -> Void
    var onOpenExternal: ((URL) -> Void)?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            ForEach(page.items) { item in
                switch item {
                case let .topicGroup(id, topic, entries):
                    Text(verbatim: UiMessages.string(.userContent(topic.name), locale: locale))
                        .font(Typography.headline)
                    ForEach(entries) { entry in
                        switch entry {
                        case let .review(memberId, review):
                            reviewButton(review, itemId: id, memberId: memberId)
                        case let .referralLink(memberId, link):
                            externalButton(
                                link.label ?? link.referralProgramName, url: link.url,
                                itemId: id, memberId: memberId
                            )
                        }
                    }
                case let .review(id, review):
                    reviewButton(review, itemId: id)
                case let .referralLink(id, link):
                    externalButton(link.label ?? link.referralProgramName, url: link.url, itemId: id)
                case let .profileLink(id, link):
                    if let type = ProfileLinkType(rawValue: link.linkType),
                       let url = type.resolvedURL(handle: link.handle, fallbackURL: link.url) {
                        externalButton(
                            link.name ?? link.handle ?? url.absoluteString,
                            url: url.absoluteString,
                            itemId: id
                        )
                    }
                case let .link(id, label, url):
                    externalButton(label, url: url, itemId: id)
                }
            }
        }
    }

    private func reviewButton(_ review: LandingPageReview, itemId: String, memberId: String? = nil) -> some View {
        Button {
            recordClick(itemId, memberId: memberId)
            onNavigate(NativeEntityDetailPath.review(id: review.id, slug: review.slug))
        } label: {
            Text(verbatim: UiMessages.string(review.landingPageReviewDisplay.text, locale: locale))
        }
        .accessibilityIdentifier("landing-item-\(memberId ?? itemId)")
    }

    @ViewBuilder
    private func externalButton(
        _ label: String, url: String, itemId: String, memberId: String? = nil
    ) -> some View {
        if let destination = URL(string: url),
           ["https", "http"].contains(destination.scheme?.lowercased() ?? ""), destination.host != nil {
            Button {
                recordClick(itemId, memberId: memberId)
                if let onOpenExternal {
                    onOpenExternal(destination)
                } else {
                    openURL(destination)
                }
            } label: {
                Text(verbatim: UiMessages.string(.userContent(label), locale: locale))
            }
            .accessibilityIdentifier("landing-item-\(memberId ?? itemId)")
        }
    }

    private func recordClick(_ itemId: String, memberId: String?) {
        viewModel.recordPublicLandingItemClick(landingPageId: page.id, itemId: itemId, groupMemberId: memberId)
    }
}
