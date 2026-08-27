import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class EntityRelationForwardPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testProfileTagFailurePreservesPageThenRetryAppendsAndMergesVotes() async throws {
        CannedFeedURLProtocol.queuedHandlers[relationsPath] = [
            (relationsPage(id: "relation-1", choice: .confirm, cursor: "profile-cursor", hasMore: true), 200, 0),
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (relationsPage(id: "relation-2", choice: .dispute, cursor: nil, hasMore: false), 200, 0)
        ]
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .userProfile), client: makeClient())
        viewModel.detailRelationEntityId = "user-2"

        try await viewModel.loadUserTags(client: makeClient())
        await viewModel.loadMoreUserTags()

        XCTAssertEqual(viewModel.detailUserTags.map(\.id), ["relation-1"])
        XCTAssertNotNil(viewModel.detailUserTagPagination.lastError)

        await viewModel.loadMoreUserTags()

        XCTAssertEqual(viewModel.detailUserTags.map(\.id), ["relation-1", "relation-2"])
        XCTAssertEqual(viewModel.detailUserTagVotes["relation-1"]?.choice, .confirm)
        XCTAssertEqual(viewModel.detailUserTagVotes["relation-2"]?.choice, .dispute)
        XCTAssertNil(viewModel.detailUserTagPagination.lastError)
        XCTAssertEqual(capturedQueries(), [
            initialQuery,
            pageQuery(cursor: "profile-cursor"),
            pageQuery(cursor: "profile-cursor")
        ])
    }

    func testManagementTagFailurePreservesPageThenRetryAppendsAndMergesVotes() async throws {
        CannedFeedURLProtocol.queuedHandlers[relationsPath] = [
            (relationsPage(id: "relation-1", choice: .confirm, cursor: "management-cursor", hasMore: true), 200, 0),
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (relationsPage(id: "relation-2", choice: .dispute, cursor: nil, hasMore: false), 200, 0)
        ]
        let viewModel = try managementViewModel()

        try await viewModel.loadRelations(client: makeClient())
        await viewModel.loadMoreRelations()

        XCTAssertEqual(viewModel.relations.map(\.id), ["relation-1"])
        XCTAssertNotNil(viewModel.relationPagination.lastError)

        await viewModel.loadMoreRelations()

        XCTAssertEqual(viewModel.relations.map(\.id), ["relation-1", "relation-2"])
        XCTAssertEqual(viewModel.electionVotes["relation-1"]?.choice, .confirm)
        XCTAssertEqual(viewModel.electionVotes["relation-2"]?.choice, .dispute)
        XCTAssertNil(viewModel.relationPagination.lastError)
        XCTAssertEqual(capturedQueries(), [
            managementInitialQuery,
            managementPageQuery(cursor: "management-cursor"),
            managementPageQuery(cursor: "management-cursor")
        ])
    }

    func testLoadRelationsWithoutActiveTabClearsPaginationRowsAndVotes() async throws {
        let viewModel = try managementViewModel()
        viewModel.relations = try decodeRelations()
        viewModel.relationPagination.reset(items: viewModel.relations)
        viewModel.relationPagination.restoreContinuation(endCursor: "stale", hasMore: true)
        let vote = try decodeVote()
        viewModel.electionVotes = ["relation-1": vote]
        viewModel.activeTab = "missing"

        try await viewModel.loadRelations(client: makeClient())

        XCTAssertTrue(viewModel.relations.isEmpty)
        XCTAssertTrue(viewModel.relationPagination.items.isEmpty)
        XCTAssertTrue(viewModel.electionVotes.isEmpty)
        XCTAssertFalse(viewModel.relationPagination.hasLoadedPage)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    private var relationsPath: String {
        "/api/v1/entity-relations/user/user-2/category/topic"
    }

    private var initialQuery: String {
        "sort=best&limit=25&positiveNetVoteScore=true"
    }

    private var managementInitialQuery: String {
        "sort=best&limit=25"
    }

    private func pageQuery(cursor: String) -> String {
        "sort=best&limit=25&after=\(cursor)&positiveNetVoteScore=true"
    }

    private func managementPageQuery(cursor: String) -> String {
        "sort=best&limit=25&after=\(cursor)"
    }

    private func capturedQueries() -> [String?] {
        CannedFeedURLProtocol.capturedURLs.filter { $0.path == relationsPath }.map(\.query)
    }

    private func managementViewModel() throws -> NativeTagManagementViewModel {
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: nil,
            subjectKind: .user,
            subjectId: "user-2"
        )
        viewModel.subjectId = "user-2"
        viewModel.tabs = nativeUserTagTabs
        viewModel.activeTab = "topic"
        return viewModel
    }

    private func relationsPage(
        id: String,
        choice: ElectionVoteChoice,
        cursor: String?,
        hasMore: Bool
    ) -> Data {
        let score = choice == .confirm ? 1 : -1
        return Data("""
        {
          "results": [{ "id": "\(id)" }],
          "page_info": { "has_next_page": \(hasMore), "end_cursor": \(json(cursor)) },
          "entity_relations": {
            "\(id)": {
              "id": "\(id)", "subject_id": "user-2", "object_id": "tag-\(id)",
              "created_at": "2026-01-01T00:00:00Z", "created_by_id": "user-1",
              "votes_score_net": \(score), "object_data": { "label": "\(id)" }
            }
          },
          "election_votes": {
            "\(id)": {
              "__entity_type": "election_vote",
              "entity_id": "\(id)",
              "user_id": "user-1",
              "choice": "\(choice.rawValue)",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """.utf8)
    }

    private func decodeRelations() throws -> [EntityRelation] {
        let response = try decoder.decode(
            EntityRelationsResponse.self,
            from: relationsPage(id: "relation-1", choice: .confirm, cursor: nil, hasMore: false)
        )
        return response.results.compactMap { response.entityRelations[$0.id] }
    }

    private func decodeVote() throws -> EntityRelationVote {
        try decoder.decode(EntityRelationVote.self, from: Data(
            #"{"__entity_type":"election_vote","entity_id":"relation-1","user_id":"user-1","choice":"confirm","created_at":"2026-01-01T00:00:00Z"}"#
                .utf8
        ))
    }

    private func json(_ value: String?) -> String {
        value.map { #""\#($0)""# } ?? "null"
    }

    private var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }
}
