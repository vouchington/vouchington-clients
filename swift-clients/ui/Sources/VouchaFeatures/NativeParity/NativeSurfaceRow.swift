import SwiftUI
import VouchaCore
import VouchaDesignSystem

struct NativeSurfaceRow: View {
    @Environment(\.locale)
    private var locale
    @Environment(\.timeZone)
    private var timeZone
    let row: NativeRouteDestinationRow
    var isStaffViewer = false
    var showStaffClient = false
    var showsLeadingIcon = true

    var body: some View {
        if let externalURL = row.externalURL {
            Link(destination: externalURL) {
                rowContent
            }
            .buttonStyle(.plain)
        } else {
            rowContent
        }
    }

    private var rowContent: some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            if let imageURL = row.avatarURL ?? row.imageURL, let url = URL(string: imageURL) {
                rowImage(url: url)
                    .accessibilityHidden(true)
                    .accessibilityIdentifier(row.avatarURL == nil ? "topic-logo-image" : "native-surface-avatar")
            } else if showsLeadingIcon {
                Image(systemName: row.icon)
                    .font(.headline)
                    .foregroundStyle(Colors.primary)
                    .frame(width: 22)
            }
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(row.localizedTitle(locale: locale, timeZone: timeZone))
                    .font(Typography.headline)
                    .authoredContentLanguage(declared: row.declaredLanguage, detected: row.detectedLanguage)
                AccountTypeBadge(accountType: row.accountType)
                ProvenanceBadge(
                    provenance: row.provenance,
                    staffProvenance: row.staffProvenance,
                    isStaffViewer: isStaffViewer,
                    showClient: showStaffClient
                )
                Text(row.localizedDetail(locale: locale, timeZone: timeZone))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .fixedSize(horizontal: false, vertical: true)
                    .authoredContentLanguage(declared: row.detailDeclaredLanguage, detected: row.detailDetectedLanguage)
            }
            Spacer(minLength: 0)
            if row.externalURL != nil {
                Image(systemName: "arrow.up.forward")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.75))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    @ViewBuilder
    private func rowImage(url: URL) -> some View {
        let image = AsyncImageView(
            urlString: url.absoluteString,
            baseURL: AppConfig.shared.imageBaseURL,
            contentMode: .fill
        )
        .frame(width: 48, height: 48)

        if row.avatarURL != nil {
            image.clipShape(Circle())
        } else {
            image.clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        }
    }
}
