import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

private actor DestinationScript {
    var posts = 0
    var lookups = 0
    var values: [String?]
    let slug: String?
    let entityId: String?
    init(slug: String? = nil, entityId: String? = "feed", values: [String?]) {
        self.slug = slug
        self.entityId = entityId
        self.values = values
    }

    func create() -> RssFeedImportSubmission {
        posts += 1
        return .init(import: destinationSummary(pending: 1), statusUrl: "/api/v1/my/import/rss-feeds/batch")
    }

    func status() -> RssFeedImportStatus {
        .init(import: destinationSummary(pending: 0), rows: [
            .init(input: "feed", status: .sourceCreated, entityId: entityId, topicSlug: slug)
        ])
    }

    func topic() throws -> String? {
        lookups += 1
        if values.isEmpty { throw URLError(.badServerResponse) }
        return values.count > 1 ? values.removeFirst() : values.first ?? nil
    }
}

private func destinationSummary(pending: Int) -> RssFeedImportSummary {
    .init(
        id: "batch",
        totalRows: 1,
        completedRows: pending == 0 ? 1 : 0,
        failedRows: 0,
        pendingRows: pending,
        completedAt: nil,
        createdAt: Date()
    )
}

@MainActor
final class SourceCreationDestinationTests: XCTestCase {
    private func makeModel(_ script: DestinationScript) -> SourceCreationViewModel {
        let model = SourceCreationViewModel(service: .init(
            create: { _ in await script.create() }, status: { _ in await script.status() },
            topic: { _ in try await script.topic() }
        ), baseURL: URL(string: "https://example.com")!, identity: { "account" }, delay: {})
        model.url = "https://example.com/feed"
        return model
    }

    func testStatusSlugRendersOpenWithoutDetailLookup() async throws {
        let script = DestinationScript(slug: "canonical-source", entityId: nil, values: [])
        let model = makeModel(script)
        await model.create()
        var path: String?
        let view = SourceCreationView(model: model, navigate: { path = $0 })
        try view.inspect().find(button: "Open source").tap()
        XCTAssertEqual(path, "/source/canonical-source")
        let counts = await (script.posts, script.lookups)
        XCTAssertEqual(counts.0, 1)
        XCTAssertEqual(counts.1, 0)
    }

    func testMissingOrInvalidLookupRemainsRenderedRetryableWithOnePost() async throws {
        for unresolved in [nil, "", "../other", "bad/slug", "bad?slug", "bad slug"] as [String?] {
            let script = DestinationScript(values: [unresolved, "resolved-topic"])
            let model = makeModel(script)
            await model.create()
            XCTAssertFalse(model.isTerminal)
            XCTAssertNil(model.currentDestination)
            let completed = expectation(description: "retry resolves destination")
            withObservationTracking { _ = model.destination } onChange: {
                Task { @MainActor in completed.fulfill() }
            }
            let view = SourceCreationView(model: model, navigate: { _ in })
            try view.inspect().find(button: "Try status again").tap()
            await fulfillment(of: [completed], timeout: 2)
            XCTAssertEqual(model.currentDestination, "/source/resolved-topic")
            XCTAssertTrue(model.isTerminal)
            let counts = await (script.posts, script.lookups)
            XCTAssertEqual(counts.0, 1)
            XCTAssertEqual(counts.1, 2)
        }
    }

    func testInvalidStatusSlugFallsBackToDetail() async {
        for slug in ["", " ", "bad/slug"] {
            let script = DestinationScript(slug: slug, values: ["safe-topic"])
            let model = makeModel(script)
            await model.create()
            XCTAssertEqual(model.currentDestination, "/source/safe-topic")
            let count = await script.lookups
            XCTAssertEqual(count, 1)
        }
    }

    func testMissingEntityAndSlugKeepsStatusRetryAvailable() async throws {
        let script = DestinationScript(entityId: nil, values: [])
        let model = makeModel(script)
        await model.create()
        XCTAssertFalse(model.isTerminal)
        XCTAssertNoThrow(try SourceCreationView(model: model, navigate: { _ in }).inspect()
            .find(button: "Try status again"))
        let counts = await (script.posts, script.lookups)
        XCTAssertEqual(counts.0, 1)
        XCTAssertEqual(counts.1, 0)
    }
}
