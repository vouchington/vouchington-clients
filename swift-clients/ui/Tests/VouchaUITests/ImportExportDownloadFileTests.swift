import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportDownloadFileTests: XCTestCase {
    func testDownloadExportUsesOwnedFileWithoutRewritingIt() async throws {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("downloaded-export-\(UUID().uuidString).json")
        let expected = Data("[\"streamed\"]".utf8)
        try expected.write(to: url)
        let service = ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            importSources: { _ in throw URLError(.badServerResponse) },
            sourceStatus: { _ in throw URLError(.badServerResponse) },
            downloadExport: { _, _, _ in url }
        )
        let viewModel = ImportExportViewModel(route: .topics, service: service)

        await viewModel.prepareExport()

        XCTAssertEqual(viewModel.exportURL, url)
        XCTAssertEqual(try Data(contentsOf: url), expected)
        viewModel.cancelOperations()
        XCTAssertFalse(FileManager.default.fileExists(atPath: url.path))
    }
}
