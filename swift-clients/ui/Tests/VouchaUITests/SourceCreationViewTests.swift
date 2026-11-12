import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
private final class CreationReadGate {
    var continuation: CheckedContinuation<RssFeedImportStatus, Never>?
    var waiting: CheckedContinuation<Void, Never>?
    func read() async -> RssFeedImportStatus {
        await withCheckedContinuation { continuation in
            self.continuation = continuation
            waiting?.resume()
            waiting = nil
        }
    }

    func wait() async {
        if continuation != nil { return }
        await withCheckedContinuation { waiting = $0 }
    }

    func release() {
        continuation?.resume(returning: .init(import: .init(
            id: "id", totalRows: 1, completedRows: 1, failedRows: 0, pendingRows: 0,
            completedAt: nil, createdAt: Date()
        ), rows: [.init(input: "feed", status: .sourceCreated, entityId: "feed")]))
        continuation = nil
    }
}

@MainActor
final class SourceCreationViewTests: XCTestCase {
    func testRenderedCreateProgressStopAndRetryControls() async throws {
        let gate = CreationReadGate()
        let accepted = RssFeedImportSubmission(import: .init(
            id: "id", totalRows: 1, completedRows: 0, failedRows: 0, pendingRows: 1,
            completedAt: nil, createdAt: Date()
        ), statusUrl: "/api/v1/my/import/rss-feeds/id")
        let model = try SourceCreationViewModel(
            service: .init(
                create: { _ in accepted },
                status: { _ in await gate.read() },
                topic: { _ in "topic" }
            ),
            baseURL: XCTUnwrap(URL(string: "https://example.com")),
            identity: { "a" },
            delay: {}
        )
        let view = SourceCreationView(model: model, navigate: { _ in }, close: {})
        try view.inspect().find(ViewType.TextField.self).setInput("https://example.com/feed")
        try view.inspect().find(button: "Create").tap()
        await gate.wait()
        XCTAssertNoThrow(try view.inspect().find(ViewType.ProgressView.self))
        try view.inspect().find(button: "Stop monitoring").tap()
        gate.release()
        try view.inspect().find(button: "Try status again").tap()
        await gate.wait()
        XCTAssertTrue(model.isWorking)
        gate.release()
        // The operation completion is observed through its own task, without a clock delay.
        for _ in 0 ..< 100 where model.isWorking {
            await Task.yield()
        }
        XCTAssertEqual(model.destination, "/source/topic")
        try view.inspect().form().callOnDisappear()
        XCTAssertFalse(model.isWorking)
    }

    func testIdentityResetClearsAppliedStateAndRejectsOlderOperation() async throws {
        let gate = CreationReadGate()
        var account = "a"
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
                statusUrl: "/api/v1/my/import/rss-feeds/id"
            ) },
            status: { _ in await gate.read() }, topic: { _ in "topic" }
        ), baseURL: XCTUnwrap(URL(string: "https://example.com")), identity: { account }, delay: {})
        model.url = "https://example.com/feed"
        let old = Task { await model.create() }
        await gate.wait()
        account = "b"
        model.reset()
        gate.release()
        await old.value
        XCTAssertNil(model.importId)
        XCTAssertNil(model.message)
        XCTAssertNil(model.result)
        XCTAssertNil(model.destination)
        XCTAssertFalse(model.submitted)
        XCTAssertFalse(model.isTerminal)
        XCTAssertEqual(model.url, "")
    }
}
