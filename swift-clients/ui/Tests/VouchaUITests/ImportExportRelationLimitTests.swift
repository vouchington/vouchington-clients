import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportRelationLimitTests: NativeRouteSurfaceViewModelTestCase {
    func testTopicSkippedResultPreservesCodeAndInputAndRendersFailure() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/import/topics"] = (
            Data(#"{"results":[{"input":"Travel","status":"skipped","error_code":"RELATION_LIMIT_REACHED"}]}"#.utf8),
            200
        )
        let model = try ImportExportViewModel(route: .topics, service: .live(client: makeClient()))
        model.inputText = "Travel"

        await model.importText()

        XCTAssertEqual(model.inputText, "Travel")
        XCTAssertEqual(model.results.first?.status, .skipped)
        XCTAssertEqual(model.results.first?.errorCode, .relationLimitReached)
        XCTAssertNil(model.errorMessage)
        let view = ImportExportView(route: .topics, viewModel: model)
        XCTAssertNoThrow(try view.inspect().find(text: "Failed: Unknown error"))
    }

    func testWholeSourceBatchWithSkippedRowsTerminatesWithoutDoubleCountingFailures() async throws {
        let summary = #"{"id":"batch","total_rows":3,"completed_rows":1,"failed_rows":2,"skipped_rows":1,"pending_rows":0,"created_at":"2026-01-01T00:00:00Z","completed_at":"2026-01-01T00:00:01Z"}"#
        CannedFeedURLProtocol.handlers["/api/v1/my/import/rss-feeds"] = (
            Data("{\"import\":\(summary),\"status_url\":\"/api/v1/my/import/rss-feeds/batch\"}".utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/import/rss-feeds/batch"] = (
            Data("""
            {"import":\(summary),"rows":[
              {"id":"1","input":"https://example.test/1","status":"followed"},
              {"id":"2","input":"https://example.test/2","status":"error","error_code":"IMPORT_FAILED"},
              {"id":"3","input":"https://example.test/3","status":"error","error_code":"RELATION_LIMIT_REACHED"}
            ],"page_info":{"has_next_page":false}}
            """.utf8), 200
        )
        let model = try ImportExportViewModel(
            route: .sources(initialExportFilter: nil), service: .live(client: makeClient())
        )
        model.inputText = "https://example.test/1\nhttps://example.test/2\nhttps://example.test/3"

        await model.importText()
        await model.monitorTask?.value

        XCTAssertEqual(model.results.map(\.status), [.followed, .error, .error])
        XCTAssertEqual(model.results.last?.errorCode, .relationLimitReached)
        XCTAssertEqual(model.sourceSummary?.skippedRows, 1)
        XCTAssertEqual(model.sourceSummary?.failedRows, 2)
        XCTAssertFalse(model.isMonitoring)
        XCTAssertFalse(model.canResumeMonitoring)
        let view = ImportExportView(route: model.route, viewModel: model)
        XCTAssertEqual(try view.inspect().find(ViewType.ProgressView.self).fractionCompleted(), 1)
        XCTAssertNoThrow(try view.inspect().find(text: "Failed: Unknown error"))
    }
}
