import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportSourceExportAcceptanceTests: XCTestCase {
    func testEverySourceFilterAndFormatCreatesExpectedNativeArtifact() async throws {
        let recorder = SourceExportRecorder()
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service()
        )
        let filters: [SourceFeedType?] = [nil, .article, .podcast, .video]

        for filter in filters {
            for format in SourceExportFormat.allCases {
                viewModel.exportFilter = filter
                viewModel.exportFormat = format
                await viewModel.prepareExport()

                let url = try XCTUnwrap(viewModel.exportURL)
                let expectedName = format == .csv ? "rss-feeds.csv" : "rss-feeds.opml"
                XCTAssertTrue(url.lastPathComponent.hasSuffix("-\(expectedName)"))
                XCTAssertEqual(try Data(contentsOf: url), SourceExportRecorder.data(filter: filter, format: format))

                if filter == nil, format == .opml {
                    let view = ImportExportView(
                        route: .sources(initialExportFilter: nil),
                        viewModel: viewModel
                    )
                    XCTAssertNoThrow(try view.inspect().find(text: "Share export"))
                }
                viewModel.cancelOperations()
                XCTAssertFalse(FileManager.default.fileExists(atPath: url.path))
            }
        }

        let calls = await recorder.calls
        XCTAssertEqual(calls, filters.flatMap { filter in
            SourceExportFormat.allCases.map { SourceExportCall(filter: filter, format: $0) }
        })
    }
}

private struct SourceExportCall: Equatable {
    let filter: SourceFeedType?
    let format: SourceExportFormat
}

private actor SourceExportRecorder {
    private(set) var calls: [SourceExportCall] = []

    nonisolated func service() -> ImportExportService {
        ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            exportTopics: { TopicExportResponse(results: []) },
            importSources: { _ in throw URLError(.badServerResponse) },
            sourceStatus: { _ in throw URLError(.badServerResponse) },
            exportSources: { filter, format in await self.export(filter: filter, format: format) }
        )
    }

    nonisolated static func data(filter: SourceFeedType?, format: SourceExportFormat) -> Data {
        Data("\(filter?.rawValue ?? "all")-\(format.rawValue)".utf8)
    }

    private func export(filter: SourceFeedType?, format: SourceExportFormat) -> Data {
        calls.append(SourceExportCall(filter: filter, format: format))
        return Self.data(filter: filter, format: format)
    }
}
