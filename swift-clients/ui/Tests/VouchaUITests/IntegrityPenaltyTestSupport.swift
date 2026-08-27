import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels

@MainActor
enum IntegrityPenaltyTestSupport {
    static func reportRows(_ fixture: String = "default") throws -> [IntegrityPenaltyRow] {
        let response: ReportIntegrityPenaltiesResponse = try decode(
            "native.moderation.report-integrity.penalties.\(fixture)"
        )
        return response.results.map(IntegrityPenaltyRow.report)
    }

    static func voteRows(_ fixture: String = "default") throws -> [IntegrityPenaltyRow] {
        let response: VoteIntegrityPenaltiesResponse = try decode(
            "native.moderation.vote-integrity.penalties.\(fixture)"
        )
        return response.results.map(IntegrityPenaltyRow.vote)
    }

    static func revokedReportRow() throws -> IntegrityPenaltyRow {
        let response: ReportIntegrityPenaltyRevokeResponse = try decode(
            "native.moderation.report-integrity.penalties.revoke"
        )
        return .report(response.penalty)
    }

    static func revokedVoteRow() throws -> IntegrityPenaltyRow {
        let response: VoteIntegrityPenaltyEnvelope = try decode(
            "native.moderation.vote-integrity.penalties.revoke"
        )
        return .vote(response.penalty)
    }

    static func page(
        _ rows: [IntegrityPenaltyRow],
        hasMore: Bool = false,
        cursor: String? = nil
    ) -> IntegrityPenaltyPage {
        .init(
            results: rows,
            pageInfo: .init(hasNextPage: hasMore, endCursor: cursor, startCursor: rows.first?.id)
        )
    }

    private static func decode<Value: Decodable>(_ id: String) throws -> Value {
        try JSONDecoder.vouchaFixtureDecoder.decode(Value.self, from: ApiFixtureLoader.data(id))
    }
}

@MainActor
final class IntegrityPenaltyServiceDouble: IntegrityPenaltyServicing {
    var pages: [Result<IntegrityPenaltyPage, Error>] = []
    var exactResults: [Result<IntegrityPenaltyRow, Error>] = []
    var revokeResults: [Result<IntegrityPenaltyRow, Error>] = []
    var calls: [(IntegrityDomain, IntegrityPenaltyStatus?, String?, Int)] = []
    var exactCalls: [(IntegrityDomain, String)] = []
    var revokeCalls: [(IntegrityDomain, String)] = []
    var pageDelays: [Duration] = []
    var exactDelay: Duration = .zero

    func penalties(
        domain: IntegrityDomain,
        status: IntegrityPenaltyStatus?,
        after: String?,
        limit: Int
    ) async throws -> IntegrityPenaltyPage {
        calls.append((domain, status, after, limit))
        let result = pages.removeFirst()
        if !pageDelays.isEmpty {
            let delay = pageDelays.removeFirst()
            if delay > .zero {
                try await Task.sleep(for: delay)
            }
        }
        return try result.get()
    }

    func penalty(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        exactCalls.append((domain, id))
        if exactDelay > .zero {
            try await Task.sleep(for: exactDelay)
        }
        return try exactResults.removeFirst().get()
    }

    func revoke(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        revokeCalls.append((domain, id))
        return try revokeResults.removeFirst().get()
    }
}
