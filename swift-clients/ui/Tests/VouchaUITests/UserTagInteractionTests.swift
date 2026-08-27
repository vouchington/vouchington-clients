import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class UserTagInteractionTests: NativeRouteSurfaceViewModelTestCase {
    func testProfileLoadsOnlyPositiveUserTags() async throws {
        CannedFeedURLProtocol.handlers[relationsPath] = (relationsData(choice: .confirm), 200)
        let viewModel = try profileViewModel()

        try await viewModel.loadUserTags(client: makeClient())

        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first)
        XCTAssertEqual(request.path, relationsPath)
        XCTAssertTrue(request.query?.contains("positiveNetVoteScore=true") == true)
        XCTAssertEqual(viewModel.detailUserTags.map(\.id), ["relation-1"])
    }

    func testProfileIgnoresConcurrentVoteForSameTagAndPreservesRowsOnFailure() async throws {
        CannedFeedURLProtocol.queuedHandlers[votePath] = [(Data("{}".utf8), 500, 0.2)]
        let viewModel = try profileViewModel()
        viewModel.detailUserTags = try decodedRelations()

        let firstVote = Task { await viewModel.voteUserTag(relationId: "relation-1", choice: .confirm) }
        try await waitForRequest(path: votePath)
        await viewModel.voteUserTag(relationId: "relation-1", choice: .dispute)
        await firstVote.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == votePath }.count, 1)
        XCTAssertEqual(viewModel.detailUserTags.map(\.id), ["relation-1"])
        XCTAssertFalse(viewModel.isVotingUserTag(relationId: "relation-1"))
    }

    func testManagementFetchesAllScoresAndRefreshesAfterAddVoteAndVoteRemoval() async throws {
        CannedFeedURLProtocol.queuedHandlers[relationsPath] = [
            (emptyRelationsData, 200, 0),
            (entityRelationResponseData, 201, 0),
            (relationsData(choice: .neutral), 200, 0),
            (relationsData(choice: .confirm), 200, 0),
            (relationsData(choice: .neutral), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[votePath] = (Data("{}".utf8), 200)
        let viewModel = try managementViewModel()

        try await viewModel.loadRelations(client: makeClient())
        XCTAssertFalse(try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first?.query).contains("positiveNetVoteScore"))

        await viewModel.addSelectedPublisherType(id: "user-tag-bot")
        await viewModel.vote(relationId: "relation-1", choice: .confirm)
        await viewModel.vote(relationId: "relation-1", choice: nil)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == relationsPath }.count, 5)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains(#"{"objectId":"user-tag-bot"}"#))
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains(#"{"choice":"confirm"}"#))
        XCTAssertEqual(viewModel.electionVotes["relation-1"]?.choice, .neutral)
    }

    func testManagementIgnoresConcurrentAddAndVoteTaps() async throws {
        CannedFeedURLProtocol.queuedHandlers[relationsPath] = [
            (entityRelationResponseData, 201, 0.2),
            (relationsData(choice: .neutral), 200, 0),
            (relationsData(choice: .confirm), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers[votePath] = [(Data("{}".utf8), 200, 0.2)]
        let viewModel = try managementViewModel()

        let firstAdd = Task { await viewModel.addSelectedPublisherType(id: "user-tag-bot") }
        try await waitForRequest(path: relationsPath)
        await viewModel.addSelectedPublisherType(id: "user-tag-bot")
        await firstAdd.value

        let firstVote = Task { await viewModel.vote(relationId: "relation-1", choice: .confirm) }
        try await waitForRequest(path: votePath)
        await viewModel.vote(relationId: "relation-1", choice: .dispute)
        await firstVote.value

        let methods = zip(CannedFeedURLProtocol.capturedURLs, CannedFeedURLProtocol.capturedMethods)
        XCTAssertEqual(methods.filter { $0.0.path == relationsPath && $0.1 == "POST" }.count, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == votePath }.count, 1)
        XCTAssertFalse(viewModel.isMutating)
    }

    func testManagementFailurePreservesLastSuccessfulTags() async throws {
        CannedFeedURLProtocol.queuedHandlers[votePath] = [(Data("{}".utf8), 500, 0)]
        let viewModel = try managementViewModel()
        viewModel.relations = try decodedRelations()

        await viewModel.vote(relationId: "relation-1", choice: .confirm)

        XCTAssertEqual(viewModel.relations.map(\.id), ["relation-1"])
        if case .error = viewModel.state {
        } else {
            XCTFail("Expected vote failure to be visible")
        }
    }

    func testManagementAddPastTagLimitSetsTagLimitReachedWithoutError() async throws {
        CannedFeedURLProtocol.handlers[relationsPath] = (Data(#"{"code":"TAG_LIMIT_REACHED"}"#.utf8), 403)
        let viewModel = try managementViewModel()

        await viewModel.addSelectedPublisherType(id: "user-tag-bot")

        XCTAssertTrue(viewModel.tagLimitReached)
        if case .error = viewModel.state {
            XCTFail("Expected tag-limit rejection to bypass the generic error state")
        }
    }

    func testUserManagementLoadsSubjectTabsCatalogAndRelations() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/user-tags"] = (userTagsData, 200)
        CannedFeedURLProtocol.handlers[relationsPath] = (relationsData(choice: .dispute), 200)
        let viewModel = try managementViewModel()

        try await viewModel.loadSubject(client: makeClient())
        try await viewModel.loadTabs(client: makeClient())
        try await viewModel.loadRelations(client: makeClient())

        XCTAssertEqual(uiEnglish(viewModel.subjectTitle), "user-2")
        XCTAssertEqual(uiEnglish(viewModel.subjectDetail), "User tags")
        XCTAssertEqual(viewModel.tabs, nativeUserTagTabs)
        XCTAssertEqual(viewModel.publisherTypes.map(\.label), ["Bot", "Spammer"])
        XCTAssertEqual(viewModel.relations.map(\.id), ["relation-1"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.map(\.path),
            ["/api/v1/topics/user-tags", relationsPath]
        )
    }

    func testSuccessfulProfileVoteAndReloadRefreshTagSummary() async throws {
        CannedFeedURLProtocol.handlers[votePath] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.queuedHandlers[relationsPath] = [
            (relationsData(choice: .confirm), 200, 0),
            (relationsData(choice: .dispute), 200, 0)
        ]
        let viewModel = try profileViewModel()

        await viewModel.voteUserTag(relationId: "relation-1", choice: .confirm)
        XCTAssertEqual(viewModel.detailUserTagVotes["relation-1"]?.choice, .confirm)

        await viewModel.reloadUserTags()
        XCTAssertEqual(viewModel.detailUserTagVotes["relation-1"]?.choice, .dispute)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == relationsPath }.count, 2)
    }

    func testUserTagViewsRenderPickerRowsAndBusyControls() throws {
        let management = try managementViewModel()
        management.publisherTypes = try decodedUserTags()
        management.relations = try decodedRelations()
        management.electionVotes = try ["relation-1": decodedVote(choice: .confirm)]
        management.inFlightMutationKeys = ["add:user-tag-bot", "vote:relation-1"]
        let surface = NativeTagManagementSurface(
            client: nil,
            routeMatch: nil,
            subjectKind: .user,
            subjectId: "user-2",
            isSignedIn: true,
            showSignIn: {}
        )

        let addForm = surface.addTagForm(viewModel: management)
        XCTAssertNoThrow(try addForm.inspect().find(ViewType.Picker.self))
        XCTAssertTrue(try NativeTagPublisherPicker(viewModel: management).inspect().find(ViewType.Picker.self)
            .isDisabled())
        XCTAssertNoThrow(try NativeTagRelationList(viewModel: management, canCreateVote: true).inspect()
            .find(text: "Bot"))
        let relationButtons = try NativeTagRelationList(viewModel: management, canCreateVote: true)
            .inspect().findAll(ViewType.Button.self)
        XCTAssertTrue(relationButtons[0].isDisabled())
        XCTAssertTrue(relationButtons[1].isDisabled())

        management.searchResults = [genericBotTag]
        let searchButton = try NativeTagSearchResults(viewModel: management).inspect().find(ViewType.Button.self)
        XCTAssertTrue(searchButton.isDisabled())
    }

    func testTagLimitCtaInvokesDefaultNoOpNavigationWhenOnNavigateOmitted() throws {
        let management = try managementViewModel()
        management.tagLimitReached = true
        let surface = NativeTagManagementSurface(
            client: nil,
            routeMatch: nil,
            subjectKind: .user,
            subjectId: "user-2",
            isSignedIn: true,
            showSignIn: {}
        )

        let addForm = surface.addTagForm(viewModel: management)
        XCTAssertNoThrow(try addForm.inspect().find(button: "View plans").tap())
    }

    func testTagLimitCtaRendersInPlaceOfFormAndNavigatesToPlans() throws {
        let management = try managementViewModel()
        management.tagLimitReached = true
        var path: String?
        let surface = NativeTagManagementSurface(
            client: nil,
            routeMatch: nil,
            subjectKind: .user,
            subjectId: "user-2",
            isSignedIn: true,
            showSignIn: {},
            onNavigate: { path = $0 }
        )

        let addForm = surface.addTagForm(viewModel: management)
        let inspection = try addForm.inspect()
        XCTAssertNoThrow(try inspection.find(text: "You've reached your tag limit"))
        XCTAssertThrowsError(try inspection.find(ViewType.Picker.self))
        try inspection.find(button: "View plans").tap()
        XCTAssertEqual(path, "/plans")
    }

    func testProfileUserTagControlsRenderAndGateSignedOutActions() throws {
        let viewModel = try profileViewModel()
        viewModel.detailRelationEntityType = "user"
        viewModel.detailUserTags = try decodedRelations()
        viewModel.detailUserTagVotes = try ["relation-1": decodedVote(choice: .confirm)]
        viewModel.inFlightUserTagVoteIds = ["relation-1"]
        var signInCount = 0
        var actionCount = 0
        let signedIn = try NativeDetailSurface(entry: entry(for: .userProfile), viewModel: viewModel)
        let signedOut = try NativeDetailSurface(
            entry: entry(for: .userProfile),
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: { signInCount += 1 }
        )

        let controls = signedIn.userTagControls
        let inspection = try controls.inspect()
        XCTAssertEqual(try inspection.find(text: "User tags").string(), "User tags")
        XCTAssertEqual(try inspection.find(text: "Bot").string(), "Bot")
        XCTAssertNoThrow(try inspection.find(button: "Manage"))
        XCTAssertTrue(try inspection.find(ViewType.Menu.self).isDisabled())

        signedOut.performSignedIn { actionCount += 1 }
        XCTAssertEqual(signInCount, 1)
        XCTAssertEqual(actionCount, 0)
    }

    func testRelationVotePermissionAllowsOfficialStructuralRelationsButRestrictsUserTags() {
        XCTAssertTrue(nativeCanCreateRelationVote(
            isSignedIn: true,
            canCastPublicVotes: false,
            isAdministrator: false,
            isUserTag: false
        ))
        XCTAssertFalse(nativeCanCreateRelationVote(
            isSignedIn: true,
            canCastPublicVotes: false,
            isAdministrator: false,
            isUserTag: true
        ))
        XCTAssertTrue(nativeCanCreateRelationVote(
            isSignedIn: true,
            canCastPublicVotes: false,
            isAdministrator: true,
            isUserTag: true
        ))
        XCTAssertFalse(nativeCanCreateRelationVote(
            isSignedIn: false,
            canCastPublicVotes: true,
            isAdministrator: true,
            isUserTag: false
        ))
    }

    private var relationsPath: String {
        "/api/v1/entity-relations/user/user-2/category/topic"
    }

    private var votePath: String {
        "/api/v1/entity-relations/relation-1/vote"
    }

    private func profileViewModel() throws -> NativeRouteSurfaceViewModel {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .userProfile), client: makeClient())
        viewModel.detailRelationEntityId = "user-2"
        return viewModel
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

    private func waitForRequest(path: String) async throws {
        for _ in 0 ..< 100 {
            if CannedFeedURLProtocol.capturedURLs.contains(where: { $0.path == path }) {
                return
            }
            try await Task.sleep(for: .milliseconds(5))
        }
        XCTFail("Timed out waiting for \(path)")
    }

    private func decodedRelations() throws -> [EntityRelation] {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode([EntityRelation].self, from: Data("""
        [{
          "id": "relation-1", "subject_id": "user-2", "object_id": "user-tag-bot",
          "created_at": "2026-01-01T00:00:00Z", "created_by_id": "user-1",
          "votes_score_net": 1, "object_data": { "name": "Bot" }
        }]
        """.utf8))
    }

    private func decodedUserTags() throws -> [PublisherTypeTopic] {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode([PublisherTypeTopic].self, from: Data(
            #"[{"id":"user-tag-bot","slug":"bot","label":"Bot"},{"id":"user-tag-spammer","slug":"spammer","label":"Spammer"}]"#
                .utf8
        ))
    }

    private func decodedVote(choice: ElectionVoteChoice) throws -> EntityRelationVote {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(EntityRelationVote.self, from: Data(
            #"{"__entity_type":"election_vote","entity_id":"relation-1","user_id":"user-1","choice":"\#(choice.rawValue)","created_at":"2026-01-01T00:00:00Z"}"#
                .utf8
        ))
    }

    private var genericBotTag: NativeGenericEntity {
        .init(
            id: "user-tag-bot",
            slug: "bot",
            name: "Bot",
            title: nil,
            username: nil,
            subject: nil,
            status: nil,
            postType: nil,
            topicType: "topic",
            feedType: nil,
            pathname: nil,
            hostname: nil,
            url: nil,
            description: nil,
            summary: nil
        )
    }

    private var userTagsData: Data {
        Data(
            #"{"user_tags":[{"id":"user-tag-bot","slug":"bot","label":"Bot"},{"id":"user-tag-spammer","slug":"spammer","label":"Spammer"}]}"#
                .utf8
        )
    }

    private var emptyRelationsData: Data {
        Data(#"{"results":[],"page_info":{"has_next_page":false},"entity_relations":{}}"#.utf8)
    }

    private func relationsData(choice: ElectionVoteChoice) -> Data {
        Data("""
        {
          "results": [{ "id": "relation-1" }],
          "page_info": { "has_next_page": false },
          "entity_relations": {
            "relation-1": {
              "id": "relation-1", "subject_id": "user-2", "object_id": "user-tag-bot",
              "created_at": "2026-01-01T00:00:00Z", "created_by_id": "user-1",
              "votes_score_net": 0, "object_data": { "label": "Bot" }
            }
          },
          "election_votes": {
            "relation-1": {
              "__entity_type": "election_vote",
              "entity_id": "relation-1",
              "user_id": "user-1",
              "choice": "\(choice.rawValue)",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """.utf8)
    }

    private var entityRelationResponseData: Data {
        Data("""
        {
          "relation": {
            "id": "relation-1", "subject_id": "user-2", "object_id": "user-tag-bot",
            "created_at": "2026-01-01T00:00:00Z", "created_by_id": "user-1",
            "votes_score_net": 0, "object_data": { "label": "Bot" }
          }
        }
        """.utf8)
    }
}
