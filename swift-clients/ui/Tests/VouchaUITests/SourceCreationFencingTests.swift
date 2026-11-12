import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
private final class CreationGate {
    var continuation: CheckedContinuation<RssFeedImportSubmission, Never>?
    var reached: CheckedContinuation<Void, Never>?
    func create() async -> RssFeedImportSubmission {
        await withCheckedContinuation { continuation in
            self.continuation = continuation
            reached?.resume()
            reached = nil
        }
    }

    func wait() async {
        if continuation != nil { return }
        await withCheckedContinuation { reached = $0 }
    }

    func release(url: String = "/api/v1/my/import/rss-feeds/id") {
        continuation?.resume(returning: .init(import: .init(
            id: "id", totalRows: 1, completedRows: 0, failedRows: 0, pendingRows: 1,
            completedAt: nil, createdAt: Date()
        ), statusUrl: url))
        continuation = nil
    }
}

@MainActor
final class SourceCreationFencingTests: XCTestCase {
    func testCancellationAndChangedAccountIgnoreLateAcceptance() async throws {
        for changeAccount in [false, true] {
            let gate = CreationGate()
            var account: String? = "first"
            let model = try SourceCreationViewModel(service: .init(
                create: { _ in await gate.create() },
                status: { _ in XCTFail("stale acceptance must not read")
                    throw URLError(.cancelled)
                },
                topic: { _ in nil }
            ), baseURL: XCTUnwrap(URL(string: "https://example.com")), identity: { account }, delay: {})
            model.url = "https://example.com/feed"
            let task = Task { await model.create() }
            await gate.wait()
            await model.create()
            if changeAccount { account = "second" }
            model.cancel()
            gate.release()
            await task.value
            await model.create()
            XCTAssertNil(model.importId)
            XCTAssertTrue(model.submitted)
            XCTAssertFalse(model.isWorking)
        }
    }

    func testInvalidAcceptanceNeverPollsOrReposts() async throws {
        let model = try SourceCreationViewModel(service: .init(
            create: { _ in .init(
                import: .init(
                    id: "id",
                    totalRows: 1,
                    completedRows: 0,
                    failedRows: 0,
                    pendingRows: 1,
                    completedAt: nil,
                    createdAt: Date()
                ),
                statusUrl: "https://evil.com/api/v1/my/import/rss-feeds/id"
            ) },
            status: { _ in XCTFail("invalid URL must not read")
                throw URLError(.badURL)
            }, topic: { _ in nil }
        ), baseURL: XCTUnwrap(URL(string: "https://example.com")), identity: { "account" }, delay: {})
        model.url = "https://example.com/feed"
        await model.create()
        await model.resume()
        XCTAssertEqual(model.importId, "id")
        XCTAssertTrue(model.isTerminal)
    }

    func testSubmissionErrorIsLocalizedAndSingleSubmit() async throws {
        let model = try SourceCreationViewModel(service: .init(
            create: { _ in throw URLError(.networkConnectionLost) },
            status: { _ in throw URLError(.badURL) }, topic: { _ in nil }
        ), baseURL: XCTUnwrap(URL(string: "https://example.com")), identity: { "account" }, delay: {})
        model.url = "https://example.com/feed"
        await model.create()
        XCTAssertTrue(model.isTerminal)
        XCTAssertNotNil(model.message)
    }
}
