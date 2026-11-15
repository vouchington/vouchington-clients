import Foundation
import VouchaModels

/// One row per account. Account, incident and review identifiers are used only to group and count.
struct NativeChatRepeatInfringerFact: Equatable, Identifiable {
    let id: String
    let username: String?
    let isOperative: Bool
    let terminationInEffect: Bool
    let hasOpenReview: Bool
    let incidentCount: Int
}

extension NativeChatMCPFact {
    static func makeRepeatInfringers(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(RepeatInfringerEnvelope.self, value),
              envelope.copyrightRepeatInfringerAccounts.count <= 500,
              envelope.copyrightRepeatInfringerAccounts.allSatisfy({
                  UUID(uuidString: $0.accountUserId) != nil && UUID(uuidString: $0.incidentId) != nil
              }) else { return nil }
        var order: [String] = []
        var grouped: [String: [RepeatInfringerRow]] = [:]
        for row in envelope.copyrightRepeatInfringerAccounts {
            if grouped[row.accountUserId] == nil { order.append(row.accountUserId) }
            grouped[row.accountUserId, default: []].append(row)
        }
        return .repeatInfringers(order.compactMap { id in grouped[id].map { fact(id: id, rows: $0) } })
    }

    private static func fact(id: String, rows: [RepeatInfringerRow]) -> NativeChatRepeatInfringerFact {
        NativeChatRepeatInfringerFact(
            id: id,
            username: rows.lazy.compactMap(\.accountUsername).first { !$0.isEmpty },
            isOperative: rows.contains(where: \.isOperative),
            terminationInEffect: rows.contains(where: \.terminationInEffect),
            hasOpenReview: rows.contains { $0.openReviewId != nil },
            incidentCount: Set(rows.map(\.incidentId)).count
        )
    }
}

private struct RepeatInfringerEnvelope: Decodable {
    let copyrightRepeatInfringerAccounts: [RepeatInfringerRow]
}

private struct RepeatInfringerRow: Decodable {
    let accountUserId: String
    let accountUsername: String?
    let incidentId: String
    let isOperative: Bool
    let openReviewId: String?
    let terminationInEffect: Bool
}
