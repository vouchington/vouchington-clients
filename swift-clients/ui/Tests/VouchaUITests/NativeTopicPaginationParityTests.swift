import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeTopicPaginationParityTests: NativeRouteSurfaceViewModelTestCase {
    func testTopicManagementViewModelLoadMoreAliasesAppendsNextPageAndUsesCursor() async throws {
        let path = "/api/v1/topics/topic-1/aliases"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Data(
                    #"""
                    {"results":["primary"],
                    "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"""
                    {"results":["secondary"],
                    "page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            )
        ]
        let aliasMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/aliases")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: aliasMatch)

        try await viewModel.reloadAliases()
        await viewModel.loadMoreAliases()

        XCTAssertEqual(viewModel.aliases, ["primary", "secondary"])
        let queryItems = CannedFeedURLProtocol.capturedURLs.last.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems
        }
        XCTAssertEqual(queryItems?.first(where: { $0.name == "after" })?.value, "cursor-1")
        XCTAssertFalse(viewModel.aliasesPagination.hasMore)
    }

    func testTopicManagementViewModelLoadMoreAdditionalHostnamesAppendsNextPageAndUsesCursor() async throws {
        let path = "/api/v1/topics/topic-1/additional-hostnames"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Data(
                    #"""
                    {"results":[{"hostname_id":"host-1","hostname":"first.example.com","topic_id":"topic-1"}],
                    "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"""
                    {"results":[{"hostname_id":"host-2","hostname":"second.example.com","topic_id":"topic-1"}],
                    "page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            )
        ]
        let domainMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/domains")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: domainMatch)

        try await viewModel.reloadAdditionalHostnames()
        await viewModel.loadMoreAdditionalHostnames()

        XCTAssertEqual(viewModel.additionalHostnames.map(\.hostname), ["first.example.com", "second.example.com"])
        let queryItems = CannedFeedURLProtocol.capturedURLs.last.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems
        }
        XCTAssertEqual(queryItems?.first(where: { $0.name == "after" })?.value, "cursor-1")
        XCTAssertFalse(viewModel.additionalHostnamesPagination.hasMore)
    }

    func testLoadMoreAliasesCancellationErrorLeavesPageIntactWithoutError() async throws {
        let path = "/api/v1/topics/topic-1/aliases"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"""
                {"results":["primary"],
                "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                """#.utf8
            ),
            200
        )
        let aliasMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/aliases")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: aliasMatch)
        try await viewModel.reloadAliases()

        CannedFeedURLProtocol.suspendResponse(path: path)
        let loadMore = Task { await viewModel.loadMoreAliases() }
        await waitForSuspendedResponse(path)
        loadMore.cancel()
        await loadMore.value

        XCTAssertEqual(viewModel.aliases, ["primary"])
        XCTAssertTrue(viewModel.aliasesPagination.hasMore)
        XCTAssertNil(viewModel.aliasesPagination.lastError)
        XCTAssertFalse(viewModel.aliasesPagination.isLoading)
    }

    func testLoadMoreAliasesVouchaErrorSetsLastError() async throws {
        let path = "/api/v1/topics/topic-1/aliases"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"""
                {"results":["primary"],
                "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                """#.utf8
            ),
            200
        )
        let aliasMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/aliases")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: aliasMatch)
        try await viewModel.reloadAliases()

        CannedFeedURLProtocol.handlers[path] = (Data(#"{"message":"offline"}"#.utf8), 503)
        await viewModel.loadMoreAliases()

        XCTAssertEqual(viewModel.aliases, ["primary"])
        guard case .api = viewModel.aliasesPagination.lastError else {
            return XCTFail(
                "Expected a VouchaError.api, got \(String(describing: viewModel.aliasesPagination.lastError))"
            )
        }
    }

    func testLoadMoreAliasesUnexpectedErrorSetsLastError() async throws {
        let path = "/api/v1/topics/topic-1/aliases"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"""
                {"results":["primary"],
                "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                """#.utf8
            ),
            200
        )
        let aliasMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/aliases")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: aliasMatch)
        try await viewModel.reloadAliases()

        CannedFeedURLProtocol.errors[path] = FlakyPaginationTransportFailure()
        await viewModel.loadMoreAliases()

        XCTAssertEqual(viewModel.aliases, ["primary"])
        guard case let .unexpected(message) = viewModel.aliasesPagination.lastError else {
            return XCTFail(
                "Expected a VouchaError.unexpected, got \(String(describing: viewModel.aliasesPagination.lastError))"
            )
        }
        // `URLProtocolClient.didFailWithError` round-trips the injected error through NSError
        // bridging, which drops `LocalizedError` conformance — the message is Foundation's
        // generic bridged description, not `FlakyPaginationTransportFailure.errorDescription`.
        XCTAssertTrue(
            message.contains("FlakyPaginationTransportFailure"),
            "Expected the bridged NSError description for FlakyPaginationTransportFailure, got \(message)"
        )
    }

    func testLoadMoreAdditionalHostnamesCancellationErrorLeavesPageIntactWithoutError() async throws {
        let path = "/api/v1/topics/topic-1/additional-hostnames"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"""
                {"results":[{"hostname_id":"host-1","hostname":"first.example.com","topic_id":"topic-1"}],
                "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                """#.utf8
            ),
            200
        )
        let domainMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/domains")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: domainMatch)
        try await viewModel.reloadAdditionalHostnames()

        CannedFeedURLProtocol.suspendResponse(path: path)
        let loadMore = Task { await viewModel.loadMoreAdditionalHostnames() }
        await waitForSuspendedResponse(path)
        loadMore.cancel()
        await loadMore.value

        XCTAssertEqual(viewModel.additionalHostnames.map(\.hostname), ["first.example.com"])
        XCTAssertTrue(viewModel.additionalHostnamesPagination.hasMore)
        XCTAssertNil(viewModel.additionalHostnamesPagination.lastError)
        XCTAssertFalse(viewModel.additionalHostnamesPagination.isLoading)
    }

    func testLoadMoreAdditionalHostnamesVouchaErrorSetsLastError() async throws {
        let path = "/api/v1/topics/topic-1/additional-hostnames"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"""
                {"results":[{"hostname_id":"host-1","hostname":"first.example.com","topic_id":"topic-1"}],
                "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                """#.utf8
            ),
            200
        )
        let domainMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/domains")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: domainMatch)
        try await viewModel.reloadAdditionalHostnames()

        CannedFeedURLProtocol.handlers[path] = (Data(#"{"message":"offline"}"#.utf8), 503)
        await viewModel.loadMoreAdditionalHostnames()

        XCTAssertEqual(viewModel.additionalHostnames.map(\.hostname), ["first.example.com"])
        guard case .api = viewModel.additionalHostnamesPagination.lastError else {
            return XCTFail(
                "Expected a VouchaError.api, got \(String(describing: viewModel.additionalHostnamesPagination.lastError))"
            )
        }
    }

    func testLoadMoreAdditionalHostnamesUnexpectedErrorSetsLastError() async throws {
        let path = "/api/v1/topics/topic-1/additional-hostnames"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"""
                {"results":[{"hostname_id":"host-1","hostname":"first.example.com","topic_id":"topic-1"}],
                "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                """#.utf8
            ),
            200
        )
        let domainMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/domains")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: domainMatch)
        try await viewModel.reloadAdditionalHostnames()

        CannedFeedURLProtocol.errors[path] = FlakyPaginationTransportFailure()
        await viewModel.loadMoreAdditionalHostnames()

        XCTAssertEqual(viewModel.additionalHostnames.map(\.hostname), ["first.example.com"])
        guard case let .unexpected(message) = viewModel.additionalHostnamesPagination.lastError else {
            return XCTFail(
                "Expected a VouchaError.unexpected, got \(String(describing: viewModel.additionalHostnamesPagination.lastError))"
            )
        }
        // See the matching alias-variant test above: the bridged NSError description replaces
        // `FlakyPaginationTransportFailure.errorDescription`, so only assert it surfaced.
        XCTAssertTrue(
            message.contains("FlakyPaginationTransportFailure"),
            "Expected the bridged NSError description for FlakyPaginationTransportFailure, got \(message)"
        )
    }

    func testAliasFieldsLoadMoreButtonInvokesViewModelPagination() async throws {
        let path = "/api/v1/topics/topic-1/aliases"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Data(
                    #"""
                    {"results":["primary"],
                    "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"""
                    {"results":["secondary"],
                    "page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            )
        ]
        let aliasMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/aliases")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: aliasMatch)
        try await viewModel.reloadAliases()

        let sut = NativeTopicManagementAliasFields(viewModel: viewModel)
        try sut.inspect().find(button: "Load more").tap()
        try await waitForAliasCount(2, in: viewModel)

        XCTAssertEqual(viewModel.aliases, ["primary", "secondary"])
    }

    func testDomainFieldsLoadMoreButtonInvokesViewModelPagination() async throws {
        let path = "/api/v1/topics/topic-1/additional-hostnames"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Data(
                    #"""
                    {"results":[{"hostname_id":"host-1","hostname":"first.example.com","topic_id":"topic-1"}],
                    "page_info":{"has_next_page":true,"end_cursor":"cursor-1","start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"""
                    {"results":[{"hostname_id":"host-2","hostname":"second.example.com","topic_id":"topic-1"}],
                    "page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
                    """#.utf8
                ),
                200,
                0
            )
        ]
        let domainMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/domains")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: domainMatch)
        try await viewModel.reloadAdditionalHostnames()

        let sut = NativeTopicManagementDomainFields(viewModel: viewModel)
        try sut.inspect().find(button: "Load more").tap()
        try await waitForAdditionalHostnameCount(2, in: viewModel)

        XCTAssertEqual(viewModel.additionalHostnames.map(\.hostname), ["first.example.com", "second.example.com"])
    }

    private func waitForAliasCount(_ expectedCount: Int, in viewModel: NativeTopicManagementViewModel) async throws {
        for _ in 0 ..< 100 {
            if viewModel.aliases.count == expectedCount {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for \(expectedCount) topic aliases")
    }

    private func waitForAdditionalHostnameCount(
        _ expectedCount: Int,
        in viewModel: NativeTopicManagementViewModel
    ) async throws {
        for _ in 0 ..< 100 {
            if viewModel.additionalHostnames.count == expectedCount {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for \(expectedCount) additional hostnames")
    }

    private func waitForSuspendedResponse(_ path: String) async {
        for _ in 0 ..< 100 where !CannedFeedURLProtocol.hasSuspendedResponse(path: path) {
            await Task.yield()
        }
        XCTAssertTrue(CannedFeedURLProtocol.hasSuspendedResponse(path: path))
    }
}

/// A transport-layer failure that is neither `CancellationError` nor bridged to `URLError`,
/// so it forces `loadMoreAliases()`/`loadMoreAdditionalHostnames()` down their generic
/// `catch { ... }` branch instead of the `VouchaError` or cancellation branches.
private struct FlakyPaginationTransportFailure: Error, LocalizedError {
    var errorDescription: String? {
        "Simulated flaky pagination transport failure."
    }
}
