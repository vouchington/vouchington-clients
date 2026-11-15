import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels

@MainActor
enum IntegrityPenaltyReviewTestSupport {
    static func dueRows(_ domain: IntegrityDomain, _ fixture: String = "review-due") throws -> IntegrityPenaltyPage {
        switch domain {
        case .report:
            let response: ReportIntegrityPenaltiesResponse = try decode(
                "native.moderation.report-integrity.penalties.\(fixture)"
            )
            return .init(results: response.results.map(IntegrityPenaltyRow.report), pageInfo: response.pageInfo)
        case .vote:
            let response: VoteIntegrityPenaltiesResponse = try decode(
                "native.moderation.vote-integrity.penalties.\(fixture)"
            )
            return .init(results: response.results.map(IntegrityPenaltyRow.vote), pageInfo: .init(
                hasNextPage: response.pageInfo.hasNextPage,
                endCursor: response.pageInfo.endCursor,
                startCursor: response.pageInfo.startCursor
            ))
        }
    }

    static func reviewed(_ domain: IntegrityDomain) throws -> IntegrityPenaltyRow {
        switch domain {
        case .report:
            let response: ReportIntegrityPenaltyEnvelope = try decode(
                "native.moderation.report-integrity.penalties.review"
            )
            return .report(response.penalty)
        case .vote:
            let response: VoteIntegrityPenaltyEnvelope = try decode(
                "native.moderation.vote-integrity.penalties.review"
            )
            return .vote(response.penalty)
        }
    }

    private static func decode<Value: Decodable>(_ id: String) throws -> Value {
        try JSONDecoder.vouchaFixtureDecoder.decode(Value.self, from: ApiFixtureLoader.data(id))
    }
}

@MainActor
final class IntegrityPenaltyReviewServiceDouble: IntegrityPenaltyReviewServicing {
    var duePages: [Result<IntegrityPenaltyPage, Error>] = []
    var reviewResults: [Result<IntegrityPenaltyRow, Error>] = []
    var exactResults: [Result<IntegrityPenaltyRow, Error>] = []
    var revokeResults: [Result<IntegrityPenaltyRow, Error>] = []
    var dueCalls: [(IntegrityDomain, String?, Int)] = []
    var reviewCalls: [(IntegrityDomain, String)] = []
    var exactCalls: [String] = []
    var revokeCalls: [String] = []

    func reviewDue(domain: IntegrityDomain, after: String?, limit: Int) async throws -> IntegrityPenaltyPage {
        dueCalls.append((domain, after, limit))
        return try duePages.removeFirst().get()
    }

    func review(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        reviewCalls.append((domain, id))
        return try reviewResults.removeFirst().get()
    }

    func penalty(domain _: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        exactCalls.append(id)
        return try exactResults.removeFirst().get()
    }

    func revoke(domain _: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        revokeCalls.append(id)
        return try revokeResults.removeFirst().get()
    }

    func penalties(
        domain _: IntegrityDomain,
        status _: IntegrityPenaltyStatus?,
        after _: String?,
        limit _: Int
    ) async throws -> IntegrityPenaltyPage {
        fatalError("ledger listing is not used by the review queue")
    }
}
