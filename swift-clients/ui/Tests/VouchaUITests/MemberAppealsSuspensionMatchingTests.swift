@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealsSuspensionMatchingTests: NativeRouteSurfaceViewModelTestCase {
    func testHistoricalSuspensionAppealDoesNotBlockCurrentSuspension() throws {
        let currentSuspensionDate = try XCTUnwrap(
            ISO8601DateFormatter().date(from: "2026-07-02T09:00:00Z")
        )
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .suspension,
            draftStore: MemberAppealDraftStore()
        )
        try viewModel.pendingAppealPagination.reset(items: [
            suspensionAppeal(id: "old-appeal", suspensionId: "old-suspension", createdAt: "2026-07-01T09:00:00Z")
        ])
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)

        XCTAssertTrue(viewModel.canAppeal(.suspension(date: currentSuspensionDate)))
    }

    func testCurrentSuspensionAppealBlocksOnlyItsExactSuspension() throws {
        let currentSuspensionDate = try XCTUnwrap(
            ISO8601DateFormatter().date(from: "2026-07-02T09:00:00Z")
        )
        let laterSuspensionDate = try XCTUnwrap(
            ISO8601DateFormatter().date(from: "2026-07-03T09:00:00Z")
        )
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .suspension,
            draftStore: MemberAppealDraftStore()
        )
        try viewModel.pendingAppealPagination.reset(items: [
            suspensionAppeal(
                id: "current-appeal",
                suspensionId: "current-suspension",
                createdAt: "2026-07-02T09:00:00Z"
            )
        ])
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)

        XCTAssertFalse(viewModel.canAppeal(.suspension(date: currentSuspensionDate)))
        XCTAssertTrue(viewModel.canAppeal(.suspension(date: laterSuspensionDate)))
    }

    func testContextlessSuspensionAppealBlocksCurrentSuspensionDuringIndependentRollout() throws {
        let currentSuspensionDate = try XCTUnwrap(
            ISO8601DateFormatter().date(from: "2026-07-02T09:00:00Z")
        )
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .suspension,
            draftStore: MemberAppealDraftStore()
        )
        try viewModel.pendingAppealPagination.reset(items: [
            suspensionAppeal(
                id: "rollout-appeal",
                suspensionId: "current-suspension",
                createdAt: "2026-07-02T09:00:00Z",
                includesTargetContext: false
            )
        ])
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)

        XCTAssertFalse(viewModel.canAppeal(.suspension(date: currentSuspensionDate)))
    }

    func testContextBearingAppealWithoutSuspensionIdDoesNotBlockCurrentSuspension() throws {
        let currentSuspensionDate = try XCTUnwrap(
            ISO8601DateFormatter().date(from: "2026-07-02T09:00:00Z")
        )
        let viewModel = MemberAppealsViewModel(
            client: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            route: .suspension,
            draftStore: MemberAppealDraftStore()
        )
        try viewModel.pendingAppealPagination.reset(items: [
            suspensionAppeal(
                id: "context-only-appeal",
                suspensionId: nil,
                contextSuspensionId: "context-only-suspension",
                createdAt: "2026-07-02T09:00:00Z"
            )
        ])
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)

        XCTAssertTrue(viewModel.canAppeal(.suspension(date: currentSuspensionDate)))
    }

    private func suspensionAppeal(
        id: String,
        suspensionId: String?,
        contextSuspensionId: String? = nil,
        createdAt: String,
        includesTargetContext: Bool = true
    ) throws -> ModerationAppeal {
        let contextId = contextSuspensionId ?? suspensionId ?? "missing-suspension"
        let context = """
        "target_context":{
          "type":"suspension","id":"\(contextId)","reason":null,"created_at":"\(createdAt)"
        }
        """
        let appeal = ModerationAppealsTestSupport.appeal(id: id, suspensionId: suspensionId)
        let json = includesTargetContext
            ? appeal.replacingOccurrences(of: #""is_overdue":true"#, with: #""is_overdue":true,\#(context)"#)
            : appeal
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(ModerationAppeal.self, from: Data(json.utf8))
    }
}
