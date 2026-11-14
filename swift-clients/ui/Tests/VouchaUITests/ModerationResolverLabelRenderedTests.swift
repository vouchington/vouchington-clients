import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class ModerationResolverLabelRenderedTests: NativeRouteSurfaceViewModelTestCase {
    private let locale = Locale.current

    private func resolvedBy() -> String {
        UiMessages.string(.nativeSwiftIntegrityResolvedBy, locale: locale)
    }

    private func notAvailable() -> String {
        UiMessages.string(.nativeSwiftRouteSurfaceNotAvailable, locale: locale)
    }

    private func staffCard(patch: ([String: Any]) -> [String: Any]) throws -> StaffModerationReportCard {
        var root = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ModerationReportsTestFixtures.staffFlat
        ) as? [String: Any])
        let first = try XCTUnwrap((root["results"] as? [[String: Any]])?.first)
        root["results"] = [patch(first)]
        let decoded = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffFlatModerationReportsResponse.self,
            from: JSONSerialization.data(withJSONObject: root)
        )
        return try StaffModerationReportCard(
            report: XCTUnwrap(decoded.reports.first),
            viewModel: ModerationReportsViewModel(client: nil, viewerTier: .administrator),
            onNavigate: { _ in },
            onConfirm: { _ in }
        )
    }

    func testReviewedReportShowsResolverDisplayNameAndReporterDisplayName() throws {
        let card = try staffCard { report in
            var report = report
            report["reviewed_at"] = "2026-06-02T12:00:00.000Z"
            report["status"] = "reviewed"
            report["resolver_display_name"] = "Mina Moderator"
            report["resolver_username"] = "mina"
            report["reporter_display_name"] = "Alice Reporter"
            return report
        }
        let inspection = try card.inspect()
        XCTAssertNoThrow(try inspection.find(text: resolvedBy()))
        XCTAssertNoThrow(try inspection.find(text: "Mina Moderator"))
        XCTAssertNoThrow(try inspection.find(text: "Reporter: Alice Reporter"))
    }

    func testReviewedReportFallsBackWhenResolverIsMissingAndPendingHidesRow() throws {
        let reviewed = try staffCard { report in
            var report = report
            report["reviewed_at"] = "2026-06-02T12:00:00.000Z"
            report["status"] = "reviewed"
            return report
        }
        XCTAssertNoThrow(try reviewed.inspect().find(text: resolvedBy()))
        XCTAssertNoThrow(try reviewed.inspect().find(text: notAvailable()))
        let pending = try staffCard { $0 }
        XCTAssertThrowsError(try pending.inspect().find(text: resolvedBy()))
    }

    private func voteFlags(resolver: String?) async throws -> [VoteIntegrityFlag] {
        var root = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.moderation.vote-integrity.default")
        ) as? [String: Any])
        var results = try XCTUnwrap(root["results"] as? [[String: Any]])
        results[1]["resolver_display_name"] = resolver ?? NSNull()
        root["results"] = results
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/flags"] = try (
            JSONSerialization.data(withJSONObject: root), 200
        )
        let model = try VoteIntegrityViewModel(
            service: APIVoteIntegrityService(client: makeClient()),
            viewerTier: .administrator
        )
        model.selectedStatus = .all
        await model.load()
        return model.flags
    }

    func testResolvedVoteFlagShowsResolverAndPendingFlagDoesNot() async throws {
        let flags = try await voteFlags(resolver: "Mina Moderator")
        let model = try VoteIntegrityViewModel(
            service: APIVoteIntegrityService(client: makeClient()), viewerTier: .administrator
        )
        let resolved = VoteIntegrityFlagCard(
            viewModel: model, flag: flags[1], onNavigate: { _ in }, onRequestPenalty: {}
        )
        XCTAssertNoThrow(try resolved.inspect().find(text: resolvedBy()))
        XCTAssertNoThrow(try resolved.inspect().find(text: "Mina Moderator"))
        let pending = VoteIntegrityFlagCard(
            viewModel: model, flag: flags[0], onNavigate: { _ in }, onRequestPenalty: {}
        )
        XCTAssertThrowsError(try pending.inspect().find(text: resolvedBy()))
        let fallback = try await voteFlags(resolver: nil)
        let unnamed = VoteIntegrityFlagCard(
            viewModel: model, flag: fallback[1], onNavigate: { _ in }, onRequestPenalty: {}
        )
        XCTAssertNoThrow(try unnamed.inspect().find(text: notAvailable()))
    }
}
