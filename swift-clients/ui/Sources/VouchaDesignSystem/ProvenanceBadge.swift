import SwiftUI
import VouchaLocalization
import VouchaModels

/// Renders only the public facts the server chose to expose.
public struct ProvenanceBadge: View {
    @Environment(\.locale)
    private var locale

    public let provenance: PublicContentProvenance?

    public init(provenance: PublicContentProvenance?) {
        self.provenance = provenance
    }

    public var body: some View {
        if let provenance {
            Text(verbatim: Self.label(for: provenance, locale: locale))
                .font(Typography.caption2)
                .foregroundStyle(Colors.secondaryLabel)
                .accessibilityIdentifier("content-provenance")
        }
    }

    public static func label(for provenance: PublicContentProvenance, locale: Locale) -> String {
        let channel = UiMessages.string(
            provenance.via == "mcp" ? .sharedProvenanceViaMcp : .sharedProvenanceViaApi,
            locale: locale
        )
        guard let app = provenance.app else { return channel }
        let name: String? = switch app.kind {
        case "hostname": app.hostname
        case "verified": app.clientName
        default: nil // The reviewed known-app catalog currently has no entries.
        }
        guard let name, !name.isEmpty else { return channel }
        return UiMessages.string(.sharedProvenanceViaApp, parameters: ["app": name], locale: locale)
    }
}
