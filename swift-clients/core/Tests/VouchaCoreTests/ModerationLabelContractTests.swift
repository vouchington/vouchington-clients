import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ModerationLabelContractTests: XCTestCase {
    func testReportAndClusterLabelsComeFromCanonicalFixtures() throws {
        let staff: StaffFlatModerationReportsResponse = try decode("native.moderation.reports.default")
        let report = try XCTUnwrap(staff.reports.first)
        XCTAssertEqual(report.targetLabel, "Native post")
        XCTAssertEqual(report.reporterUsername, "alice")
        XCTAssertNil(report.reporterDisplayName)
        XCTAssertNil(report.resolverUsername)
        XCTAssertNil(report.resolverDisplayName)
        let member: MemberFlatModerationReportsResponse = try decode("native.moderation.reports.member.default")
        XCTAssertEqual(member.reports.first?.targetLabel, "Native post")
        let clusters: StaffClusteredModerationReportsResponse =
            try decode("native.moderation.reports.clustered.default")
        XCTAssertTrue(clusters.clusters.allSatisfy { !$0.targetLabel.isEmpty })
        let context = try XCTUnwrap(clusters.clusters.flatMap(\.reports).compactMap(\.communityBanEvasion).first)
        XCTAssertEqual(context.sourceUsername, "previously-banned")
        XCTAssertNil(context.sourceDisplayName)
    }

    func testClaimReadsAndMutationsPreserveLabels() throws {
        let pending: PendingTopicClaimsResponse = try decode("native.moderation.topic-claims.pending")
        let claim = try XCTUnwrap(pending.claims.first)
        XCTAssertFalse(claim.topicName.isEmpty)
        XCTAssertEqual(claim.claimantUsername, "claimant")
        XCTAssertNil(claim.claimantDisplayName)
        for mutation in ["verification", "rejection", "revocation"] {
            let response: TopicClaimResponse = try decode("native.moderation.topic-claims.\(mutation)")
            XCTAssertEqual(response.claim.topicName, claim.topicName)
            XCTAssertEqual(response.claim.claimantUsername, claim.claimantUsername)
        }
    }

    func testNoteReadAndCreateIncludeUnavailableIdentities() throws {
        let response: UserModerationNotesResponse = try decode("native.moderation.mod-notes.default")
        let available = try XCTUnwrap(response.notes.first)
        XCTAssertEqual(available.authorUsername, "moderator")
        XCTAssertEqual(available.communityName, "Fixture community")
        XCTAssertNil(available.authorDisplayName)
        let unavailable = try XCTUnwrap(response.notes.last)
        XCTAssertNil(unavailable.authorUsername)
        XCTAssertNil(unavailable.authorDisplayName)
        XCTAssertNil(unavailable.communityName)
        let created: UserModerationNoteResponse = try decode("native.moderation.mod-notes.create.default")
        XCTAssertEqual(created.note.authorUsername, available.authorUsername)
        XCTAssertEqual(created.note.communityName, available.communityName)
    }

    func testDisputeAndReviewQueueLabelsAreRequired() throws {
        let list: ReviewDisputeListResponse = try decode("native.moderation.disputes.default")
        let detail: ReviewDisputeResponse = try decode("native.moderation.disputes.detail.default")
        XCTAssertFalse(try XCTUnwrap(list.disputes.first).postLabel.isEmpty)
        XCTAssertFalse(detail.dispute.postLabel.isEmpty)
        let review: AdminReviewQueueResponse = try decode("native.moderation.review-queue.default")
        XCTAssertTrue(review.results.allSatisfy { !$0.postLabel.isEmpty })
        try assertRequiredLabel(
            ReviewDisputeResponse.self,
            fixture: "native.moderation.disputes.detail.default",
            envelope: "dispute",
            field: "post_label"
        )
        try assertRequiredLabel(
            StaffFlatModerationReportsResponse.self,
            fixture: "native.moderation.reports.default",
            envelope: "results",
            field: "target_label"
        )
        try assertRequiredLabel(
            ReportIntegrityFlagsResponse.self,
            fixture: "native.moderation.report-integrity.default",
            envelope: "results",
            field: "entity_label"
        )
    }

    func testIntegrityLabelsAndNullablePenaltyCreators() throws {
        let report: ReportIntegrityFlagsResponse = try decode("native.moderation.report-integrity.default")
        let vote: VoteIntegrityFlagsResponse = try decode("native.moderation.vote-integrity.default")
        XCTAssertTrue(report.results.allSatisfy { !$0.entityLabel.isEmpty })
        XCTAssertTrue(vote.results.allSatisfy { !$0.entityLabel.isEmpty })
        let penalties: VoteIntegrityPenaltiesResponse = try decode("native.moderation.vote-integrity.penalties.default")
        XCTAssertEqual(penalties.results.first?.userUsername, "penalty-recipient")
        XCTAssertEqual(penalties.results.first?.creatorUsername, "moderator")
        XCTAssertNil(penalties.results.first?.revokerUsername)
        let reportPenalties: ReportIntegrityPenaltiesResponse = try decode(
            "native.moderation.report-integrity.penalties.default"
        )
        let deleted = try XCTUnwrap(reportPenalties.results.first { $0.createdById == nil })
        XCTAssertNil(deleted.createdById)
        XCTAssertNil(deleted.creatorUsername)
        XCTAssertNil(deleted.creatorDisplayName)
    }

    private func decode<T: Decodable>(_ id: String) throws -> T {
        try JSONDecoder.vouchaFixtureDecoder.decode(T.self, from: ApiFixtureLoader.data(id))
    }

    private func assertRequiredLabel(
        _ type: (some Decodable).Type, fixture: String, envelope: String, field: String
    ) throws {
        let data = ApiFixtureLoader.data(fixture)
        let source = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        for missing in [true, false] {
            var object = source
            if var entries = object[envelope] as? [[String: Any]] {
                if missing { entries[0].removeValue(forKey: field) } else { entries[0][field] = NSNull() }
                object[envelope] = entries
            } else {
                var entry = try XCTUnwrap(object[envelope] as? [String: Any])
                if missing { entry.removeValue(forKey: field) } else { entry[field] = NSNull() }
                object[envelope] = entry
            }
            XCTAssertThrowsError(try JSONDecoder.vouchaFixtureDecoder.decode(
                type, from: JSONSerialization.data(withJSONObject: object)
            ), "\(fixture) must reject \(missing ? "missing" : "null") \(field)")
        }
    }
}
