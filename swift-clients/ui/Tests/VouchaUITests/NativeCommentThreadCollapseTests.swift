import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadCollapseTests: NativeRouteSurfaceViewModelTestCase {
    private let root = CommentFixture.root
    private let hidden = CommentFixture.a
    private var rootPath: String {
        "/api/v1/posts/\(root)/descendants"
    }

    private var hiddenPath: String {
        "/api/v1/posts/\(hidden)/descendants"
    }

    private var bookmarkPath: String {
        "/api/v1/bookmarks/post/\(hidden)/hide"
    }

    func testServerHiddenRowsRemainAndExpansionUsesIndependentOpaqueCursor() async throws {
        let model = try seededModel(rootCursor: "root+/opaque=")
        CannedFeedURLProtocol.queuedHandlers[hiddenPath] = try [
            (page([CommentFixture.b], cursor: "hidden+/opaque="), 200, 0),
            (page([CommentFixture.b, CommentFixture.c]), 200, 0)
        ]
        await model.loadThread()
        XCTAssertEqual(model.descendantPosts.map(\.id), [hidden])
        XCTAssertEqual(model.collapsedCommentIds, [hidden])
        XCTAssertEqual(model.postMetricsById[hidden]?.count.children, 1)
        XCTAssertTrue(model.commentTree.first?.children.isEmpty == true)

        await model.toggleCollapse(commentId: hidden)
        XCTAssertFalse(model.collapsedCommentIds.contains(hidden))
        XCTAssertEqual(model.bookmarksByPostId[hidden]?["hide"], false)
        XCTAssertEqual(model.descendantPosts.map(\.id), [hidden, CommentFixture.b])
        await model.loadMoreHiddenDescendants(commentId: hidden)
        CannedFeedURLProtocol.handlers[rootPath] = try (page([CommentFixture.c]), 200)
        await model.loadMoreDescendants()
        XCTAssertEqual(model.descendantPosts.map(\.id), [hidden, CommentFixture.b, CommentFixture.c])
        XCTAssertEqual(model.descendantPosts.filter { $0.id == CommentFixture.b }.count, 1)
        let requests = CannedFeedURLProtocol.capturedRequests
        XCTAssertEqual(requests.filter { $0.url.path == bookmarkPath }.map(\.method), ["DELETE"])
        let hiddenQuery = requests.last { $0.url.path == hiddenPath }.flatMap { URLComponents(
            url: $0.url,
            resolvingAgainstBaseURL: false
        ) }
        XCTAssertEqual(hiddenQuery?.queryItems?.first { $0.name == "after" }?.value, "hidden+/opaque=")
        let rootQuery = requests.last { $0.url.path == rootPath }.flatMap { URLComponents(
            url: $0.url,
            resolvingAgainstBaseURL: false
        ) }
        XCTAssertEqual(rootQuery?.queryItems?.first { $0.name == "after" }?.value, "root+/opaque=")

        await model.toggleCollapse(commentId: hidden)
        XCTAssertEqual(model.collapsedCommentIds, [hidden])
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.last?.method, "PUT")
        XCTAssertEqual(model.descendantPosts.count, 3)
    }

    func testFailedBookmarkRetainsCollapseAndSubtreeFailureCanRetry() async throws {
        let model = try seededModel()
        await model.loadThread()
        CannedFeedURLProtocol.handlers[bookmarkPath] = (Data("{}".utf8), 503)
        await model.toggleCollapse(commentId: hidden)
        XCTAssertEqual(model.collapsedCommentIds, [hidden])
        XCTAssertFalse(CannedFeedURLProtocol.hasCapturedRequest(path: hiddenPath))
        CannedFeedURLProtocol.handlers[bookmarkPath] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.queuedHandlers[hiddenPath] = try [
            (Data("{}".utf8), 503, 0), (page([CommentFixture.b]), 200, 0)
        ]
        await model.toggleCollapse(commentId: hidden)
        XCTAssertTrue(model.collapsedCommentIds.isEmpty)
        XCTAssertNotNil(model.subtreePagination[hidden]?.lastError)
        await model.loadMoreHiddenDescendants(commentId: hidden)
        XCTAssertNil(model.subtreePagination[hidden]?.lastError)
        XCTAssertEqual(model.descendantPosts.map(\.id), [hidden, CommentFixture.b])
    }

    func testDuplicateToggleAndInvalidatedSessionCannotApplyLateBookmark() async throws {
        let model = try seededModel()
        await model.loadThread()
        let suspended = expectation(description: "bookmark suspended")
        CannedFeedURLProtocol.suspendResponse(path: bookmarkPath) { suspended.fulfill() }
        let toggle = Task { await model.toggleCollapse(commentId: hidden) }
        await fulfillment(of: [suspended], timeout: 2)
        await model.toggleCollapse(commentId: hidden)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(bookmarkPath), 1)
        model.invalidateSession()
        CannedFeedURLProtocol.releaseResponse(path: bookmarkPath)
        await toggle.value
        XCTAssertTrue(model.collapsedCommentIds.isEmpty)
        XCTAssertTrue(model.bookmarksByPostId.isEmpty)
        XCTAssertFalse(CannedFeedURLProtocol.hasCapturedRequest(path: hiddenPath))
    }

    func testReloadDiscardsOldSubtreeResponseAndMaintainsServerCollapse() async throws {
        let model = try seededModel()
        await model.loadThread()
        CannedFeedURLProtocol.handlers[hiddenPath] = try (page([CommentFixture.b]), 200)
        let suspended = expectation(description: "subtree suspended")
        CannedFeedURLProtocol.suspendResponse(path: hiddenPath) { suspended.fulfill() }
        let expansion = Task { await model.toggleCollapse(commentId: hidden) }
        await fulfillment(of: [suspended], timeout: 2)
        await model.loadThread()
        CannedFeedURLProtocol.releaseResponse(path: hiddenPath)
        await expansion.value
        XCTAssertEqual(model.descendantPosts.map(\.id), [hidden])
        XCTAssertEqual(model.collapsedCommentIds, [hidden])
        XCTAssertTrue(model.subtreePagination.isEmpty)
    }

    func testSignedOutPersistenceNeverReadsOrWritesSignedInUserKey() async throws {
        let suite = "NativeCommentThreadCollapseTests-\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        let key = "comments-collapsed:\(root)"
        defaults.set(["comment-d"], forKey: key)
        defaults.set(["private-old-comment"], forKey: "\(key):user-1")
        let anonymous = NativeCommentThreadViewModel(client: nil, rootPostId: root, collapseDefaults: defaults)
        XCTAssertEqual(anonymous.collapsedCommentIds, ["comment-d"])
        await anonymous.toggleCollapse(commentId: hidden)
        XCTAssertEqual(Set(defaults.stringArray(forKey: key) ?? []), [hidden, "comment-d"])
        let signedIn = NativeCommentThreadViewModel(
            client: nil,
            rootPostId: root,
            currentUserId: "user-1",
            collapseDefaults: defaults
        )
        let unknownSignedInUser = NativeCommentThreadViewModel(
            client: nil, rootPostId: root, collapseDefaults: defaults, isSignedIn: true
        )
        XCTAssertNil(unknownSignedInUser.collapseStorageKey)
        XCTAssertTrue(unknownSignedInUser.collapsedCommentIds.isEmpty)
        await unknownSignedInUser.toggleCollapse(commentId: hidden)
        XCTAssertTrue(unknownSignedInUser.collapsedCommentIds.isEmpty)
        XCTAssertNil(signedIn.collapseStorageKey)
        XCTAssertTrue(signedIn.collapsedCommentIds.isEmpty)
        await signedIn.toggleCollapse(commentId: hidden)
        XCTAssertEqual(defaults.stringArray(forKey: "\(key):user-1"), ["private-old-comment"])
    }

    func testPermalinkAcceptsPlaceholderAncestorAndHydrationPreservesState() async throws {
        let model = try seededModel()
        var ancestor = try fixtureObject("native.comments.ancestors.permalink")
        var posts = try XCTUnwrap(ancestor["posts"] as? [String: [String: Any]])
        posts[hidden]?["placeholder_state"] = "unmoderated"
        ancestor["posts"] = posts
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(CommentFixture.b)/ancestors"] = try (
            JSONSerialization.data(withJSONObject: ancestor),
            200
        )
        await model.loadPermalink(targetCommentId: CommentFixture.b)
        XCTAssertEqual(model.ancestorPosts.first?.placeholderState, "unmoderated")
        XCTAssertEqual(
            model.ancestorPosts.first?.contentPresentation(viewerId: "user-1", isStaffViewer: true),
            .unavailable
        )
        if case .loaded = model.ancestorState {} else { XCTFail("Ancestor placeholder should load") }
    }

    func testAccountTransitionInvalidatesRetiredSurfaceModel() {
        let surface = NativeCommentThreadSurface(
            client: nil, routeMatch: nil, currentUserId: "user-1", isSignedIn: true,
            showSignIn: {}
        )
        let retired = surface.viewModel
        retired.bookmarksByPostId = [hidden: ["hide": true]]
        surface.replaceCommentThreadModel(userId: nil, signedIn: false)
        XCTAssertFalse(retired.sessionIsActive)
        XCTAssertTrue(retired.bookmarksByPostId.isEmpty)
        let signedOut = NativeCommentThreadSurface(client: nil, routeMatch: nil, isSignedIn: false, showSignIn: {})
        XCTAssertNotEqual(surface.loadTaskId, signedOut.loadTaskId)
    }

    private func seededModel(rootCursor: String? = nil) throws -> NativeCommentThreadViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(root)"] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"),
            200
        )
        CannedFeedURLProtocol.handlers[rootPath] = try (page([hidden], hidden: true, cursor: rootCursor), 200)
        CannedFeedURLProtocol.handlers[bookmarkPath] = (Data("{}".utf8), 200)
        return try NativeCommentThreadViewModel(client: makeClient(), rootPostId: root, currentUserId: "user-1")
    }

    private func page(_ ids: [String], hidden: Bool = false, cursor: String? = nil) throws -> Data {
        var object = try fixtureObject("native.comments.descendants.default")
        object["results"] = ids.map { ["entity_id": $0] }
        object["page_info"] = ["has_next_page": cursor != nil, "end_cursor": cursor as Any? ?? NSNull()]
        object["bookmarks"] = [self.hidden: ["hide": hidden, "save": true]]
        return try JSONSerialization.data(withJSONObject: object)
    }

    private func fixtureObject(_ id: String) throws -> [String: Any] {
        try XCTUnwrap(JSONSerialization.jsonObject(with: ApiFixtureLoader.data(id)) as? [String: Any])
    }
}
