import Foundation
@testable import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FollowerDistributionViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    override func tearDown() {
        CannedFeedURLProtocol.discardPendingResponses()
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    func testSelectedRecipientsCapAtOneHundredAndCanBeDeselected() {
        let viewModel = makeViewModel()
        viewModel.sendsToAllFollowers = false

        for index in 0 ..< FollowerDistributionRequest.maximumSelectedRecipients {
            viewModel.toggleRecipient(String(format: "01900000-0000-7000-8000-%012d", index))
        }
        viewModel.toggleRecipient("01900000-0000-7000-8000-000000009999")

        XCTAssertEqual(viewModel.selectedRecipientIds.count, FollowerDistributionRequest.maximumSelectedRecipients)
        XCTAssertFalse(viewModel.selectedRecipientIds.contains("01900000-0000-7000-8000-000000009999"))
        viewModel.toggleRecipient("01900000-0000-7000-8000-000000000000")
        XCTAssertEqual(viewModel.selectedRecipientIds.count, FollowerDistributionRequest.maximumSelectedRecipients - 1)
    }

    func testRecipientAtSelectionLimitRemainsSelectableOnlyForRemoval() {
        let viewModel = makeViewModel()
        viewModel.sendsToAllFollowers = false
        for index in 0 ..< FollowerDistributionRequest.maximumSelectedRecipients {
            viewModel.toggleRecipient(recipientId(index))
        }

        XCTAssertTrue(viewModel.canToggleRecipient(recipientId(0)))
        XCTAssertFalse(viewModel.canToggleRecipient(recipientId(101)))
    }

    func testContextChangeResetsSelectionAndSearchState() async {
        let viewModel = makeViewModel()
        viewModel.sendsToAllFollowers = false
        viewModel.toggleRecipient("01900000-0000-7000-8000-000000000001")
        await viewModel.searchRecipients(query: "alex")

        viewModel.replaceContext(currentUserId: "viewer-2", target: .rssFeedItem("item-2"))

        XCTAssertTrue(viewModel.selectedRecipientIds.isEmpty)
        XCTAssertEqual(viewModel.query, "")
        XCTAssertTrue(viewModel.recipients.isEmpty)
        XCTAssertTrue(viewModel.sendsToAllFollowers)
        XCTAssertFalse(viewModel.canLoadMoreRecipients)
        XCTAssertNil(viewModel.error)
    }

    func testResetSendSheetClearsTransientAudienceAndSearchState() async {
        let path = "/api/v1/users/viewer-1/users/followers"
        CannedFeedURLProtocol.handlers[path] = (followersPage(hasMore: true, endCursor: "cursor-1"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-1/sends"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        viewModel.sendsToAllFollowers = false
        viewModel.toggleRecipient(recipientId(1))

        await viewModel.searchRecipients(query: "alex")
        let didSend = await viewModel.send()
        XCTAssertFalse(didSend)
        XCTAssertNotNil(viewModel.error)
        viewModel.resetSendSheet()

        XCTAssertTrue(viewModel.sendsToAllFollowers)
        XCTAssertTrue(viewModel.selectedRecipientIds.isEmpty)
        XCTAssertTrue(viewModel.selectedRecipients.isEmpty)
        XCTAssertEqual(viewModel.query, "")
        XCTAssertTrue(viewModel.recipients.isEmpty)
        XCTAssertFalse(viewModel.canLoadMoreRecipients)
        XCTAssertFalse(viewModel.isSearching)
        XCTAssertNil(viewModel.error)
    }

    func testContextChangeInvalidatesPendingShareAndUnblocksNewTarget() async throws {
        let postPath = "/api/v1/posts/post-1/shares"
        let rssPath = "/api/v1/rss-feed-items/item-2/shares"
        CannedFeedURLProtocol.handlers[postPath] = (acceptedResponse(), 202)
        CannedFeedURLProtocol.handlers[rssPath] = (acceptedResponse(), 202)
        CannedFeedURLProtocol.suspendResponse(path: postPath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: postPath, method: "POST")
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        let staleShare = Task { await viewModel.share() }
        _ = try await barrier.wait()
        XCTAssertTrue(viewModel.isSharing)

        viewModel.replaceContext(currentUserId: "viewer-2", target: .rssFeedItem("item-2"))
        XCTAssertFalse(viewModel.isSharing)
        let didShareWithNewTarget = await viewModel.share()
        XCTAssertTrue(didShareWithNewTarget)

        CannedFeedURLProtocol.releaseResponse(path: postPath)
        let didCompleteStaleShare = await staleShare.value
        XCTAssertFalse(didCompleteStaleShare)
        XCTAssertFalse(viewModel.isSharing)
        XCTAssertNil(viewModel.error)
    }

    func testContextChangeInvalidatesPendingSendAndUnblocksNewTarget() async throws {
        let postPath = "/api/v1/posts/post-1/sends"
        let rssPath = "/api/v1/rss-feed-items/item-2/sends"
        CannedFeedURLProtocol.handlers[postPath] = (acceptedResponse(), 202)
        CannedFeedURLProtocol.handlers[rssPath] = (acceptedResponse(), 202)
        CannedFeedURLProtocol.suspendResponse(path: postPath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: postPath, method: "POST")
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        let staleSend = Task { await viewModel.send() }
        _ = try await barrier.wait()
        XCTAssertTrue(viewModel.isSending)

        viewModel.replaceContext(currentUserId: "viewer-2", target: .rssFeedItem("item-2"))
        XCTAssertFalse(viewModel.isSending)
        let didSendWithNewTarget = await viewModel.send()
        XCTAssertTrue(didSendWithNewTarget)

        CannedFeedURLProtocol.releaseResponse(path: postPath)
        let didCompleteStaleSend = await staleSend.value
        XCTAssertFalse(didCompleteStaleSend)
        XCTAssertFalse(viewModel.isSending)
        XCTAssertNil(viewModel.error)
    }

    func testSelectedRecipientsPersistAcrossSearchResultReplacementAndPages() async {
        let path = "/api/v1/users/viewer-1/users/followers"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (followersPage(ids: [recipientId(1)], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (followersPage(ids: [recipientId(2)]), 200, 0),
            (followersPage(ids: [recipientId(3)]), 200, 0)
        ]
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        viewModel.sendsToAllFollowers = false

        await viewModel.searchRecipients(query: "alex")
        viewModel.toggleRecipient(recipientId(1))
        await viewModel.loadMoreRecipients()
        viewModel.toggleRecipient(recipientId(2))
        await viewModel.searchRecipients(query: "bea")

        XCTAssertEqual(viewModel.recipients.map(\.id), [recipientId(3)])
        XCTAssertEqual(viewModel.selectedRecipientIds, [recipientId(1), recipientId(2)])
        XCTAssertEqual(viewModel.selectedRecipients.map(\.id), [recipientId(1), recipientId(2)])
        viewModel.toggleRecipient(recipientId(1))
        XCTAssertEqual(viewModel.selectedRecipients.map(\.id), [recipientId(2)])
    }

    func testSearchDebouncesAndOnlyLatestQueryIssuesRequest() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/users/viewer-1/users/followers": (ApiFixtureLoader.data("swift.users.followers.default"), 200)
        ]
        CannedFeedURLProtocol.capturedURLs = []
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        let superseded = Task { await viewModel.searchRecipients(query: "first") }
        try await Task.sleep(for: .milliseconds(50))
        await viewModel.searchRecipients(query: "second")
        await superseded.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
        let query = try URLComponents(
            url: XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first),
            resolvingAgainstBaseURL: false
        )
        XCTAssertEqual(query?.queryItems?.first(where: { $0.name == "q" })?.value, "second")
    }

    func testCommentPostsCannotBeDistributedToFollowers() {
        let comment = makePost(postType: .comment, parentId: nil)
        let post = makePost(postType: .discussion, parentId: nil)

        XCTAssertFalse(canDistributeToFollowers(comment, currentUserId: "viewer-1", isSignedIn: true))
        XCTAssertTrue(canDistributeToFollowers(post, currentUserId: "viewer-1", isSignedIn: true))
    }

    func testEligibilityRejectsEveryDisallowedPostState() {
        let eligible = makePost(postType: .discussion, parentId: nil)
        let ownPost = makePost(postType: .discussion, parentId: nil, createdById: "viewer-1")
        let child = makePost(postType: .discussion, parentId: "parent-1")
        let privatePost = makePost(postType: .discussion, parentId: nil, privacy: .private)
        let followersOnly = makePost(postType: .discussion, parentId: nil, broadcast: .followers)

        XCTAssertFalse(canDistributeToFollowers(eligible, currentUserId: nil, isSignedIn: true))
        XCTAssertFalse(canDistributeToFollowers(eligible, currentUserId: "viewer-1", isSignedIn: false))
        XCTAssertFalse(canDistributeToFollowers(ownPost, currentUserId: "viewer-1", isSignedIn: true))
        XCTAssertFalse(canDistributeToFollowers(child, currentUserId: "viewer-1", isSignedIn: true))
        XCTAssertFalse(canDistributeToFollowers(privatePost, currentUserId: "viewer-1", isSignedIn: true))
        XCTAssertFalse(canDistributeToFollowers(followersOnly, currentUserId: "viewer-1", isSignedIn: true))
        XCTAssertTrue(canDistributeToFollowers(eligible, currentUserId: "viewer-1", isSignedIn: true))
        XCTAssertTrue(canDistributeToFollowers(
            makePost(postType: .discussion, parentId: nil, broadcast: .users),
            currentUserId: "viewer-1",
            isSignedIn: true
        ))
    }

    func testSearchTrimsQueryDeduplicatesAndLoadsNextPage() async {
        let path = "/api/v1/users/viewer-1/users/followers"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (followersPage(ids: [recipientId(1), recipientId(2)], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (followersPage(ids: [recipientId(2), recipientId(3)]), 200, 0)
        ]
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        await viewModel.searchRecipients(query: "  alex  ")
        XCTAssertEqual(viewModel.query, "alex")
        XCTAssertEqual(viewModel.recipients.map(\.id), [recipientId(1), recipientId(2)])
        XCTAssertTrue(viewModel.canLoadMoreRecipients)

        await viewModel.loadMoreRecipients()

        let requests = CannedFeedURLProtocol.capturedRequests
        XCTAssertEqual(requests.map(\.url.path), [path, path])
        XCTAssertEqual(queryValue(named: "q", in: requests[0].url), "alex")
        XCTAssertEqual(queryValue(named: "after", in: requests[1].url), "cursor-1")
        XCTAssertEqual(viewModel.recipients.map(\.id), [recipientId(1), recipientId(2), recipientId(3)])
        XCTAssertFalse(viewModel.canLoadMoreRecipients)
        XCTAssertFalse(viewModel.isSearching)
    }

    func testSearchWithoutViewerAndLoadMoreWithoutCursorDoNotRequest() async {
        let viewModel = makeViewModel()
        viewModel.replaceContext(currentUserId: nil, target: .post("post-1"))

        await viewModel.searchRecipients(query: "alex")
        await viewModel.loadMoreRecipients()

        XCTAssertEqual(viewModel.query, "alex")
        XCTAssertTrue(viewModel.recipients.isEmpty)
        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.isEmpty)
    }

    func testSearchFailureSetsErrorAndClearErrorRemovesIt() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/viewer-1/users/followers"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        await viewModel.searchRecipients(query: "alex")

        XCTAssertNotNil(viewModel.error)
        XCTAssertFalse(viewModel.isSearching)
        viewModel.clearError()
        XCTAssertNil(viewModel.error)
    }

    func testShareSuccessAndFailureUseTheirTargetEndpoints() async {
        let successPath = "/api/v1/posts/post-1/shares"
        CannedFeedURLProtocol.handlers[successPath] = (acceptedResponse(), 202)
        let postViewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        let didSharePost = await postViewModel.share()
        XCTAssertTrue(didSharePost)
        XCTAssertFalse(postViewModel.isSharing)
        XCTAssertNil(postViewModel.error)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.last?.url.path, successPath)

        let failurePath = "/api/v1/rss-feed-items/item-1/shares"
        CannedFeedURLProtocol.handlers[failurePath] = (Data("{}".utf8), 500)
        let rssViewModel = makeViewModel(
            protocolClasses: [CannedFeedURLProtocol.self],
            target: .rssFeedItem("item-1")
        )

        let didShareRss = await rssViewModel.share()
        XCTAssertFalse(didShareRss)
        XCTAssertFalse(rssViewModel.isSharing)
        XCTAssertNotNil(rssViewModel.error)
        rssViewModel.clearError()
        XCTAssertNil(rssViewModel.error)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.last?.url.path, failurePath)
    }

    func testSendUsesAllAndSelectedAudienceBodies() async throws {
        let path = "/api/v1/posts/post-1/sends"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (acceptedResponse(), 202, 0),
            (acceptedResponse(), 202, 0)
        ]
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        let didSendToAll = await viewModel.send()
        XCTAssertTrue(didSendToAll)
        viewModel.sendsToAllFollowers = false
        viewModel.toggleRecipient(recipientId(2))
        viewModel.toggleRecipient(recipientId(1))
        let didSendToSelected = await viewModel.send()
        XCTAssertTrue(didSendToSelected)

        let requests = CannedFeedURLProtocol.capturedRequests
        XCTAssertEqual(requests.map(\.method), ["POST", "POST"])
        XCTAssertEqual(requests.map(\.url.path), [path, path])
        XCTAssertEqual(requests[0].body, #"{"audience":"all_followers"}"#)
        let selectedBody = try requestJSON(requests[1].body)
        XCTAssertEqual(selectedBody["audience"] as? String, "selected_followers")
        XCTAssertEqual(selectedBody["recipient_user_ids"] as? [String], [recipientId(1), recipientId(2)])
        XCTAssertFalse(viewModel.isSending)
        XCTAssertNil(viewModel.error)
    }

    func testSendRejectsEmptySelectionAndInvalidRecipientWithoutRequest() async {
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        viewModel.sendsToAllFollowers = false

        let didSendToEmptySelection = await viewModel.send()
        XCTAssertFalse(didSendToEmptySelection)
        XCTAssertNil(viewModel.error)

        viewModel.toggleRecipient("not-a-uuid")
        let didSendToInvalidSelection = await viewModel.send()
        XCTAssertFalse(didSendToInvalidSelection)
        XCTAssertNotNil(viewModel.error)
        XCTAssertFalse(viewModel.isSending)
        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.isEmpty)
    }

    private func makeViewModel(
        protocolClasses: [AnyClass] = [FailingURLProtocol.self],
        currentUserId: String? = "viewer-1",
        target: FollowerDistributionTarget = .post("post-1")
    )
        -> FollowerDistributionViewModel {
        let client = APIClient(
            config: .init(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test"),
            cookieStorage: .init(),
            protocolClasses: protocolClasses
        )
        return .init(client: client, currentUserId: currentUserId, target: target)
    }

    private func makePost(
        postType: PostType,
        parentId: String?,
        createdById: String? = "author-1",
        broadcast: BroadcastScope? = .everyone,
        privacy: PostPrivacy = .public
    ) -> Post {
        Post(
            id: "post-1",
            slug: nil,
            postType: postType,
            title: nil,
            markdown: nil,
            html: nil,
            parentId: parentId,
            rootId: nil,
            createdById: createdById,
            createdAt: .now,
            broadcast: broadcast,
            privacy: privacy,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil
        )
    }

    private func followersPage(
        ids: [String] = ["01900000-0000-7000-8000-000000000001"],
        hasMore: Bool = false,
        endCursor: String? = nil
    ) -> Data {
        let users = ids.enumerated().map { index, id in
            #"{"id":"\#(id)","username":"follower\#(index)"}"#
        }.joined(separator: ",")
        let cursor = endCursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"results":[\#(users)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(cursor)}}"#.utf8
        )
    }

    private func acceptedResponse() -> Data {
        Data(#"{"status":"accepted","distribution_id":"distribution-1"}"#.utf8)
    }

    private func recipientId(_ number: Int) -> String {
        String(format: "01900000-0000-7000-8000-%012d", number)
    }

    private func queryValue(named name: String, in url: URL) -> String? {
        URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == name }?.value
    }

    private func requestJSON(_ body: String?) throws -> [String: Any] {
        let data = try Data(XCTUnwrap(body).utf8)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    }
}
