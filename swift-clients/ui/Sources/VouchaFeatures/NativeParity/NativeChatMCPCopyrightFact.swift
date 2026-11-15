import Foundation
import VouchaModels

/// Public case labels only. Notice ids, claimant identities beyond the display name and correspondence
/// are never decoded.
struct NativeChatCopyrightNoticeFact: Equatable, Identifiable {
    let id: String
    let claimant: String?
    let jurisdiction: CopyrightNoticeJurisdiction
    let receivedAt: Date
    let workDescription: String?
}

extension NativeChatMCPFact {
    static func makeCopyrightNotices(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(CopyrightEnvelope.self, value),
              envelope.copyrightNotices.count <= 100,
              Set(envelope.copyrightNotices.map(\.id)).count == envelope.copyrightNotices.count,
              envelope.copyrightNotices.allSatisfy({
                  UUID(uuidString: $0.id) != nil && [.usDMCA, .euDSA].contains($0.jurisdiction)
              }) else { return nil }
        return .copyrightNotices(envelope.copyrightNotices.map {
            NativeChatCopyrightNoticeFact(
                id: $0.id, claimant: $0.claimantDisplayName.nonBlank, jurisdiction: $0.jurisdiction,
                receivedAt: $0.receivedAt, workDescription: $0.workDescription.nonBlank
            )
        })
    }
}

private extension String? {
    var nonBlank: String? {
        guard let value = self, !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        return value
    }
}

private struct CopyrightEnvelope: Decodable {
    let copyrightNotices: [CopyrightRow]
}

private struct CopyrightRow: Decodable {
    let id: String
    let claimantDisplayName: String?
    let jurisdiction: CopyrightNoticeJurisdiction
    let receivedAt: Date
    let workDescription: String?
}
