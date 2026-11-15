import VouchaAPI
import VouchaModels

@MainActor
protocol IntegrityPenaltyReviewServicing: IntegrityPenaltyServicing {
    func reviewDue(domain: IntegrityDomain, after: String?, limit: Int) async throws -> IntegrityPenaltyPage
    func review(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow
}

extension APIIntegrityPenaltyService: IntegrityPenaltyReviewServicing {
    func reviewDue(domain: IntegrityDomain, after: String?, limit: Int) async throws -> IntegrityPenaltyPage {
        switch domain {
        case .report:
            let response: ReportIntegrityPenaltiesResponse = try await client.send(
                .reportIntegrityPenaltiesReviewDue(after: after, limit: limit)
            )
            return .init(results: response.results.map(IntegrityPenaltyRow.report), pageInfo: response.pageInfo)
        case .vote:
            let response: VoteIntegrityPenaltiesResponse = try await client.send(
                .voteIntegrityPenaltiesReviewDue(after: after, limit: limit)
            )
            return .init(results: response.results.map(IntegrityPenaltyRow.vote), pageInfo: .init(
                hasNextPage: response.pageInfo.hasNextPage,
                endCursor: response.pageInfo.endCursor,
                startCursor: response.pageInfo.startCursor
            ))
        }
    }

    func review(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        switch domain {
        case .report:
            let response: ReportIntegrityPenaltyEnvelope = try await client.send(
                .reviewReportIntegrityPenalty(id: id)
            )
            return .report(response.penalty)
        case .vote:
            let response: VoteIntegrityPenaltyEnvelope = try await client.send(
                .reviewVoteIntegrityPenalty(id: id)
            )
            return .vote(response.penalty)
        }
    }
}
