import SwiftUI
import VouchaLocalization
import VouchaModels

/// Renders public facts, plus server-provided raw facts for the current staff viewer.
public struct ProvenanceBadge: View {
    @Environment(\.locale)
    private var locale

    public let provenance: PublicContentProvenance?
    public let staffProvenance: StaffContentProvenance?
    public let isStaffViewer: Bool
    public let showClient: Bool

    public init(
        provenance: PublicContentProvenance?,
        staffProvenance: StaffContentProvenance? = nil,
        isStaffViewer: Bool = false,
        showClient: Bool = false
    ) {
        self.provenance = provenance
        self.staffProvenance = staffProvenance
        self.isStaffViewer = isStaffViewer
        self.showClient = showClient
    }

    public var body: some View {
        if let provenance {
            Text(verbatim: Self.label(for: provenance, locale: locale))
                .font(Typography.caption2)
                .foregroundStyle(Colors.secondaryLabel)
                .accessibilityIdentifier("content-provenance")
        }
        if isStaffViewer, let staffProvenance {
            Text(verbatim: UiMessages.string(
                UiMessage(
                    .sharedProvenanceChannel,
                    textParameters: ["channel": .protocolValue(staffProvenance.createdVia)]
                ),
                locale: locale
            ))
            .font(Typography.caption2)
            .foregroundStyle(Colors.secondaryLabel)
            .accessibilityIdentifier("content-staff-provenance-channel")
            if showClient, let client = staffProvenance.oauthClient {
                Text(verbatim: UiMessages.string(
                    UiMessage(.sharedProvenanceClient, textParameters: [
                        "name": .userContent(client.clientName),
                        "clientId": .protocolValue(client.clientId)
                    ]),
                    locale: locale
                ))
                .font(Typography.caption2)
                .foregroundStyle(Colors.secondaryLabel)
                .accessibilityIdentifier("content-staff-provenance-client")
                Text(verbatim: UiMessages.string(
                    client.verified ? .sharedProvenanceVerified : .sharedProvenanceUnverified,
                    locale: locale
                ))
                .font(Typography.caption2)
                .foregroundStyle(Colors.secondaryLabel)
                .accessibilityIdentifier("content-staff-provenance-verification")
            }
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
        return UiMessages.string(
            UiMessage(.sharedProvenanceViaApp, textParameters: [
                "app": app.kind == "hostname" ? .protocolValue(name) : .userContent(name)
            ]),
            locale: locale
        )
    }
}
