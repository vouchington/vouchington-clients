@testable import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class VoteIntegrityViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testAllFilterUsesMixedDefaultFixtureWithoutStatusQuery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/flags"] = (
            ApiFixtureLoader.data("native.moderation.vote-integrity.default"),
            200
        )
        let viewModel = try VoteIntegrityViewModel(
            service: APIVoteIntegrityService(client: makeClient()),
            viewerTier: .administrator
        )
        viewModel.selectedStatus = .all

        await viewModel.load()

        XCTAssertEqual(viewModel.flags.map(\.resolution), [nil, .dismissed])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25")
    }

    func testAPIServiceUsesFixtureBackedListResolutionAndPenaltyEndpoints() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/flags"] = (
            ApiFixtureLoader.data("native.moderation.vote-integrity.pending"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/flags/vote-flag-1"] = (
            ApiFixtureLoader.data("native.moderation.vote-integrity.resolution.suspended"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/flags/vote-flag-1/penalties"] = (
            ApiFixtureLoader.data("native.moderation.vote-integrity.penalty"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/penalties"] = (
            exactPenaltySnapshotData(flagId: "vote-flag-1"),
            200
        )
        let viewModel = try VoteIntegrityViewModel(
            service: APIVoteIntegrityService(client: makeClient()),
            viewerTier: .administrator
        )

        await viewModel.load()
        let flag = try XCTUnwrap(viewModel.flags.first)
        await viewModel.applyPenalty(flag)
        await viewModel.resolve(flag, as: .suspended)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25&status=pending")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET", "POST", "PATCH"])
        XCTAssertEqual(viewModel.penalizedUserCounts[flag.id], 2)
        XCTAssertTrue(viewModel.flags.isEmpty)
    }

    func testAPIServiceSnapshotsPenaltyIdsAgainstExactFlagScope() async throws {
        let flagId = "00000000-0000-7000-8000-000000000790"
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/penalties"] = (
            exactPenaltySnapshotData(flagId: flagId),
            200
        )
        let client = try makeClient()
        let service = APIVoteIntegrityService(client: client)

        let ids = try await service.penaltyIds(flagId: flagId)

        XCTAssertTrue(ids.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.first?.query,
            "source=flag&limit=100&source_flag_id=\(flagId)"
        )
    }

    func testAPIServiceAggregatesPenaltySnapshotPagesAndForwardsOpaqueCursor() async throws {
        let flagId = "00000000-0000-7000-8000-000000000790"
        let cursor = "opaque+cursor/one="
        CannedFeedURLProtocol.queuedHandlers["/api/v1/vote-integrity/penalties"] = [
            (exactPenaltySnapshotData(
                flagId: flagId,
                ids: ["old-revoked"],
                hasNextPage: true,
                endCursor: cursor
            ), 200, 0),
            (exactPenaltySnapshotData(flagId: flagId, ids: ["new-committed"]), 200, 0)
        ]
        let client = try makeClient()
        let service = APIVoteIntegrityService(client: client)

        let ids = try await service.penaltyIds(flagId: flagId)

        XCTAssertEqual(ids, ["old-revoked", "new-committed"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET"])
        let continuationURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let continuationItems = try XCTUnwrap(
            URLComponents(url: continuationURL, resolvingAgainstBaseURL: false)?.queryItems
        )
        XCTAssertEqual(continuationItems.first(where: { $0.name == "after" })?.value, cursor)
    }

    func testMismatchedContinuationScopePreventsPenaltyPost() async throws {
        let flagId = "00000000-0000-7000-8000-000000000790"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/vote-integrity/penalties"] = [
            (exactPenaltySnapshotData(
                flagId: flagId,
                ids: ["old-revoked"],
                hasNextPage: true,
                endCursor: "next"
            ), 200, 0),
            (exactPenaltySnapshotData(
                flagId: "00000000-0000-7000-8000-000000000791",
                ids: ["untrusted-row"]
            ), 200, 0)
        ]
        let viewModel = try makeVoteIntegrityViewModel(flagId: flagId)
        let flag = try XCTUnwrap(viewModel.flags.first)

        await viewModel.applyPenalty(flag)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET"])
        XCTAssertNil(viewModel.penaltyBaselineIdsByFlagId[flagId])
        XCTAssertFalse(viewModel.confirmedPenaltyFlagIds.contains(flagId))
    }

    func testMissingPenaltySnapshotCursorPreventsPost() async throws {
        let flagId = "00000000-0000-7000-8000-000000000790"
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/penalties"] = (
            exactPenaltySnapshotData(flagId: flagId, hasNextPage: true),
            200
        )
        let viewModel = try makeVoteIntegrityViewModel(flagId: flagId)
        let flag = try XCTUnwrap(viewModel.flags.first)

        await viewModel.applyPenalty(flag)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
        XCTAssertNil(viewModel.penaltyBaselineIdsByFlagId[flagId])
        XCTAssertFalse(viewModel.confirmedPenaltyFlagIds.contains(flagId))
    }

    func testRepeatedPenaltySnapshotCursorPreventsPost() async throws {
        let flagId = "00000000-0000-7000-8000-000000000790"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/vote-integrity/penalties"] = [
            (exactPenaltySnapshotData(
                flagId: flagId,
                hasNextPage: true,
                endCursor: "repeated"
            ), 200, 0),
            (exactPenaltySnapshotData(
                flagId: flagId,
                hasNextPage: true,
                endCursor: "repeated"
            ), 200, 0)
        ]
        let viewModel = try makeVoteIntegrityViewModel(flagId: flagId)
        let flag = try XCTUnwrap(viewModel.flags.first)

        await viewModel.applyPenalty(flag)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "GET"])
        XCTAssertNil(viewModel.penaltyBaselineIdsByFlagId[flagId])
        XCTAssertFalse(viewModel.confirmedPenaltyFlagIds.contains(flagId))
    }

    func testMismatchedPenaltyBaselineScopePreventsPost() async throws {
        let flagId = "00000000-0000-7000-8000-000000000790"
        CannedFeedURLProtocol.handlers["/api/v1/vote-integrity/penalties"] = (
            exactPenaltySnapshotData(flagId: "00000000-0000-7000-8000-000000000791"),
            200
        )
        let client = try makeClient()
        let service = APIVoteIntegrityService(client: client)
        let flag = try IntegrityTestSupport.voteFlag(id: flagId)
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]

        await viewModel.applyPenalty(flag)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
        XCTAssertFalse(viewModel.confirmedPenaltyFlagIds.contains(flagId))
        XCTAssertFalse(viewModel.ambiguousPenaltyFlagIds.contains(flagId))
        XCTAssertNil(viewModel.penaltyBaselineIdsByFlagId[flagId])
    }

    func testFiltersPaginationAndDuplicateContinuationGuard() async throws {
        let service = VoteIntegrityServiceDouble()
        service.pages = try [
            .init(
                result: .success(IntegrityTestSupport.votePage(
                    [IntegrityTestSupport.voteFlag(id: "one")],
                    hasMore: true,
                    cursor: "opaque/vote+cursor"
                )),
                delay: .zero
            ),
            .init(
                result: .success(IntegrityTestSupport.votePage([
                    IntegrityTestSupport.voteFlag(id: "one"),
                    IntegrityTestSupport.voteFlag(id: "two")
                ])),
                delay: .milliseconds(50)
            ),
            .init(
                result: .success(IntegrityTestSupport.votePage([
                    IntegrityTestSupport.voteFlag(id: "resolved", resolution: "suspended")
                ])),
                delay: .zero
            )
        ]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        await viewModel.load()

        async let first: Void = viewModel.loadMore()
        await Task.yield()
        async let duplicate: Void = viewModel.loadMore()
        _ = await (first, duplicate)

        XCTAssertEqual(viewModel.flags.map(\.id), ["one", "two"])
        XCTAssertEqual(service.calls.count, 2)
        XCTAssertEqual(service.calls[1].1, "opaque/vote+cursor")
        XCTAssertEqual(service.calls[1].2, 25)

        await viewModel.selectStatus(.resolved)
        XCTAssertEqual(viewModel.flags.map(\.id), ["resolved"])
        XCTAssertEqual(service.calls.last?.0, .resolved)
    }

    func testRejectsStaleResponseAndPreservesRowsOnContinuationFailure() async throws {
        let service = VoteIntegrityServiceDouble()
        service.pages = try [
            .init(
                result: .success(IntegrityTestSupport.votePage([
                    IntegrityTestSupport.voteFlag(id: "stale")
                ])),
                delay: .milliseconds(80)
            ),
            .init(
                result: .success(IntegrityTestSupport.votePage(
                    [IntegrityTestSupport.voteFlag(id: "current", resolution: "dismissed")],
                    hasMore: true,
                    cursor: "next"
                )),
                delay: .zero
            ),
            .init(result: .failure(VouchaError.unexpected("More failed.")), delay: .zero),
            .init(
                result: .success(IntegrityTestSupport.votePage([
                    IntegrityTestSupport.voteFlag(id: "all")
                ])),
                delay: .zero
            )
        ]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)

        let stale = Task { await viewModel.load() }
        await Task.yield()
        await viewModel.selectStatus(.resolved)
        await stale.value
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.flags.map(\.id), ["current"])
        XCTAssertEqual(viewModel.continuationErrorMessage, "More failed.")

        await viewModel.selectStatus(.all)
        XCTAssertEqual(viewModel.flags.map(\.id), ["all"])
        XCTAssertNil(service.calls.last?.0)
    }

    func testEveryPatchResolutionUsesAuthoritativeReturnedFlag() async throws {
        for resolution in VoteIntegrityResolution.allCases {
            let pending = try IntegrityTestSupport.voteFlag(id: resolution.rawValue)
            let resolved = try IntegrityTestSupport.voteFlag(
                id: resolution.rawValue,
                resolution: resolution.rawValue
            )
            let service = VoteIntegrityServiceDouble()
            service.resolutionResults = [.success(resolved)]
            let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
            viewModel.selectedStatus = .all
            viewModel.flags = [pending]

            await viewModel.resolve(pending, as: resolution)

            XCTAssertEqual(viewModel.flags.first?.resolution, resolution)
            XCTAssertEqual(service.resolutionCalls.first?.0, pending.id)
            XCTAssertEqual(service.resolutionCalls.first?.1, resolution)
        }
    }

    func testResolutionRetriesAndLocksOnlyTheSameFlag() async throws {
        let first = try IntegrityTestSupport.voteFlag(id: "first")
        let second = try IntegrityTestSupport.voteFlag(id: "second")
        let resolvedFirst = try IntegrityTestSupport.voteFlag(id: "first", resolution: "dismissed")
        let resolvedSecond = try IntegrityTestSupport.voteFlag(id: "second", resolution: "suspended")
        let service = VoteIntegrityServiceDouble()
        service.resolutionDelay = .milliseconds(50)
        service.resolutionResultsByFlagId = [
            first.id: [
                .failure(VouchaError.unexpected("Resolve failed.")),
                .success(resolvedFirst)
            ],
            second.id: [.success(resolvedSecond)]
        ]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.selectedStatus = .all
        viewModel.flags = [first, second]

        async let failed: Void = viewModel.resolve(first, as: .dismissed)
        await waitForResolutionCall(service)
        async let duplicate: Void = viewModel.resolve(first, as: .suspended)
        async let otherFlag: Void = viewModel.resolve(second, as: .suspended)
        _ = await (failed, duplicate, otherFlag)

        XCTAssertEqual(service.resolutionCalls.count, 2)
        XCTAssertEqual(viewModel.mutationErrorMessages[first.id], "Resolve failed.")
        XCTAssertEqual(viewModel.flags.first(where: { $0.id == second.id })?.resolution, .suspended)

        await viewModel.resolve(first, as: .dismissed)
        XCTAssertEqual(viewModel.flags.first(where: { $0.id == first.id })?.resolution, .dismissed)
    }

    func testPenaltyReturnsCountWithoutResolvingOrReplacingFlag() async throws {
        let pending = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        service.penaltyIdResults = [.success([]), .success([])]
        service.penaltyResults = try [
            .failure(VouchaError.unexpected("Penalty failed.")),
            .success(IntegrityTestSupport.votePenalty(count: 4))
        ]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [pending]

        await viewModel.applyPenalty(pending)
        XCTAssertEqual(viewModel.mutationErrorMessages[pending.id], "Penalty failed.")
        XCTAssertNil(viewModel.flags.first?.resolution)

        await viewModel.applyPenalty(pending)
        XCTAssertEqual(viewModel.penalizedUserCounts[pending.id], 4)
        XCTAssertNil(viewModel.flags.first?.resolution)
        XCTAssertEqual(viewModel.flags.first?.id, pending.id)
    }

    func testOnlyAdministratorsCanLoadOrMutate() async throws {
        for tier in [
            IntegrityViewerTier.anonymous,
            .member,
            .siteModerator,
            .customerSupport
        ] {
            let service = VoteIntegrityServiceDouble()
            let viewModel = VoteIntegrityViewModel(service: service, viewerTier: tier)
            let flag = try IntegrityTestSupport.voteFlag()
            viewModel.flags = [flag]

            await viewModel.load()
            await viewModel.resolve(flag, as: .dismissed)
            await viewModel.applyPenalty(flag)

            XCTAssertTrue(service.calls.isEmpty)
            XCTAssertTrue(service.resolutionCalls.isEmpty)
            XCTAssertTrue(service.penaltyCalls.isEmpty)
        }
    }

    private func waitForResolutionCall(_ service: VoteIntegrityServiceDouble) async {
        for _ in 0 ..< 100 where service.resolutionCalls.isEmpty {
            await Task.yield()
        }
    }

    private func makeVoteIntegrityViewModel(flagId: String) throws -> VoteIntegrityViewModel {
        let client = try makeClient()
        let service = APIVoteIntegrityService(client: client)
        let flag = try IntegrityTestSupport.voteFlag(id: flagId)
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]
        return viewModel
    }

    private func exactPenaltySnapshotData(
        flagId: String,
        ids: [String] = [],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) -> Data {
        let results = ids.map { id in
            """
            {
              "id":"\(id)",
              "user_id":"00000000-0000-7000-8000-000000000001",
              "penalty_multiplier":0.2,
              "reason":"voting_ring",
              "source_flag_id":"\(flagId)",
              "created_by_id":"00000000-0000-7000-8000-000000000002",
              "revoked_at":"2026-07-01T12:00:00.000Z",
              "revoked_by_id":"00000000-0000-7000-8000-000000000003",
              "created_at":"2026-07-01T10:00:00.000Z"
            }
            """
        }.joined(separator: ",")
        let encodedEndCursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {
          "results":[\(results)],
          "page_info":{
            "has_next_page":\(hasNextPage),
            "end_cursor":\(encodedEndCursor),
            "start_cursor":null
          },
          "filter_scope":{"source":"flag","source_flag_id":"\(flagId)"}
        }
        """.utf8)
    }
}
