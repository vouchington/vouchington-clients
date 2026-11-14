import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeChatProfileBioFactView: View {
    let bio: NativeChatProfileBioFact
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftProfileBio, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            NativeHtmlContent(html: nil, fallback: bio.markdown, font: Typography.caption)
        }
        .accessibilityIdentifier("native-chat-profile-bio-fact")
    }
}

struct NativeChatProfileLinksFactView: View {
    let links: [NativeChatProfileLinkFact]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftSettingsProfileLinks, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            ForEach(links) { link in
                HStack(spacing: Spacing.xs) {
                    Text(UiMessages.string(link.linkType.titleKey, locale: locale))
                        .foregroundStyle(Colors.secondaryLabel)
                    if let destination = link.safeDestination {
                        Link(destination: destination) {
                            Text(verbatim: label(for: link, destination: destination))
                        }
                    } else if let value = [link.name, link.handle, link.url]
                        .compactMap(displayable).first {
                        Text(verbatim: UiMessages.string(.userContent(value), locale: locale))
                    }
                }
                .accessibilityElement(children: .combine)
            }
        }
        .accessibilityIdentifier("native-chat-profile-links-fact")
    }

    private func label(for link: NativeChatProfileLinkFact, destination: URL) -> String {
        let value = displayable(link.name)
            ?? displayable(link.handle)
            ?? destination.host ?? ""
        return UiMessages.string(.userContent(value), locale: locale)
    }

    private func displayable(_ value: String?) -> String? {
        value.flatMap { $0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : $0 }
    }
}
