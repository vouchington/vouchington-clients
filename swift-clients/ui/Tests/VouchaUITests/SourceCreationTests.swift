import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

private actor SourceCreationScript {
    var posts = 0
    var reads = 0
    var statuses: [RssFeedImportStatus]
    var failRead = false
    init(_ statuses: [RssFeedImportStatus]) {
        self.statuses = statuses
    }

    func create() -> RssFeedImportSubmission {
        posts += 1
        return .init(import: summary(pending: 1), statusUrl: "/api/v1/my/import/rss-feeds/import-1")
    }

    func read() throws -> RssFeedImportStatus {
        reads += 1
        if failRead { failRead = false
            throw URLError(.networkConnectionLost)
        }
        return statuses.count > 1 ? statuses.removeFirst() : statuses[0]
    }

    func failNext() {
        failRead = true
    }
}

private func summary(pending: Int, failed: Int = 0) -> RssFeedImportSummary {
    .init(
        id: "import-1",
        totalRows: 1,
        completedRows: pending == 0 && failed == 0 ? 1 : 0,
        failedRows: failed,
        pendingRows: pending,
        completedAt: nil,
        createdAt: Date(timeIntervalSince1970: 0)
    )
}

private func status(_ state: ImportResultStatus, pending: Int, failed: Int = 0) -> RssFeedImportStatus {
    .init(import: summary(pending: pending, failed: failed), rows: [
        .init(
            input: "https://example.com/feed",
            status: state,
            entityId: state == .sourceCreated ? "feed-1" : nil,
            errorCode: state == .pending ? .feedFetchTimeout : nil
        )
    ])
}

@MainActor
final class SourceCreationTests: XCTestCase {
    let base = URL(string: "https://example.com")!

    private func model(
        _ script: SourceCreationScript,
        limit: Int = 3,
        identity: @escaping @MainActor () -> String? = { "account" }
    ) -> SourceCreationViewModel {
        let service = SourceCreationService(
            create: { _ in await script.create() },
            status: { _ in try await script.read() },
            topic: { _ in "topic-1" }
        )
        let model = SourceCreationViewModel(
            service: service,
            baseURL: base,
            identity: identity,
            pollLimit: limit,
            delay: {}
        )
        model.url = "https://example.com/feed"
        return model
    }

    func testPendingCompletionAndOneSubmission() async {
        let script = SourceCreationScript([status(.pending, pending: 1), status(.sourceCreated, pending: 0)])
        let model = model(script)
        await model.create()
        await model.create()
        await model.resume()
        XCTAssertEqual(model.destination, "/source/topic-1")
        XCTAssertEqual(model.result?.entityId, "feed-1")
        XCTAssertTrue(model.isTerminal)
        let counts = await (script.posts, script.reads)
        XCTAssertEqual(counts.0, 1)
        XCTAssertEqual(counts.1, 2)
    }

    func testReadFailureAndBoundedPauseResumeNeverPostAgain() async {
        let script = SourceCreationScript([status(.pending, pending: 1)])
        await script.failNext()
        let model = model(script, limit: 2)
        await model.create()
        XCTAssertFalse(model.isTerminal)
        XCTAssertFalse(model.isWorking)
        await model.resume()
        XCTAssertTrue(model.result?.isRetrying == true)
        XCTAssertFalse(model.isTerminal)
        let counts = await (script.posts, script.reads)
        XCTAssertEqual(counts.0, 1)
        XCTAssertEqual(counts.1, 3)
    }

    func testDefaultPollingDelayReachesCompletion() async {
        let script = SourceCreationScript([status(.pending, pending: 1), status(.sourceCreated, pending: 0)])
        let model = SourceCreationViewModel(service: .init(
            create: { _ in await script.create() }, status: { _ in try await script.read() }, topic: { _ in "topic" }
        ), baseURL: base, identity: { "account" })
        model.url = "https://example.com/feed"
        await model.create()
        XCTAssertEqual(model.destination, "/source/topic")
    }

    func testMismatchedStatusIdentityRemainsRetryable() async {
        let wrong = RssFeedImportStatus(import: .init(
            id: "wrong", totalRows: 1, completedRows: 0, failedRows: 0, pendingRows: 1,
            completedAt: nil, createdAt: Date()
        ), rows: [])
        let script = SourceCreationScript([wrong])
        let model = model(script)
        await model.create()
        XCTAssertNil(model.result)
        XCTAssertFalse(model.isTerminal)
        XCTAssertNotNil(model.message)
    }

    func testPendingErrorRowWinsOverTerminalSummaryDuringReconciliation() async {
        let script = SourceCreationScript([status(.pending, pending: 0)])
        let model = model(script, limit: 1)
        await model.create()
        XCTAssertFalse(model.isTerminal)
        XCTAssertTrue(model.result?.isRetrying == true)
    }

    func testTerminalFailure() async {
        let model = model(SourceCreationScript([status(.error, pending: 0, failed: 1)]))
        await model.create()
        XCTAssertTrue(model.isTerminal)
        XCTAssertNil(model.destination)
        XCTAssertNotNil(model.message)
    }

    func testStatusURLValidation() {
        let path = "/api/v1/my/import/rss-feeds/import-1"
        for valid in [path, "https://example.com" + path, "https://example.com:443" + path] {
            XCTAssertTrue(SourceCreationService.validStatusURL(valid, importId: "import-1", baseURL: base), valid)
        }
        for invalid in [
            "https://evil.com" + path,
            "//example.com" + path,
            path + "?x=1",
            path + "#x",
            "https://user@example.com" + path,
            path + "/",
            path.replacingOccurrences(
                of: "import-1",
                with: "%69mport-1"
            ),
            "http://example.com" + path,
            "https://example.com:44" + path
        ] {
            XCTAssertFalse(SourceCreationService.validStatusURL(invalid, importId: "import-1", baseURL: base), invalid)
        }
        XCTAssertFalse(SourceCreationService.validStatusURL(path, importId: "", baseURL: base))
        XCTAssertFalse(SourceCreationService.validStatusURL(path, importId: "x/y", baseURL: base))
    }

    func testSignedOutAndEmptyInputDoNotSubmit() async {
        let script = SourceCreationScript([])
        let signedOut = model(script, identity: { nil })
        await signedOut.create()
        let empty = model(script)
        empty.url = " "
        await empty.create()
        await empty.resume()
        let count = await script.posts
        XCTAssertEqual(count, 0)
    }

    func testRenderedCompletionNavigation() async throws {
        let model = model(SourceCreationScript([status(.sourceCreated, pending: 0)]))
        await model.create()
        var destination: String?
        let view = SourceCreationView(model: model, navigate: { destination = $0 })
        XCTAssertNoThrow(try view.inspect().find(text: "Source created"))
        try view.inspect().find(button: "Open source").tap()
        XCTAssertEqual(destination, "/source/topic-1")
        XCTAssertTrue(try view.inspect().find(button: "Create").isDisabled())
    }
}
