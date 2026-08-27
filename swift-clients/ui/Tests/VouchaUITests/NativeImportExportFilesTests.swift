import Foundation
import UniformTypeIdentifiers
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeImportExportFilesTests: XCTestCase {
    func testSourceImportTypesIncludeStableOPMLWithoutAllowingPlainText() {
        let types = ImportExportView.sourceImportContentTypes

        XCTAssertTrue(types.contains(.vouchaOPML))
        XCTAssertTrue(UTType.vouchaOPML.conforms(to: .xml))
        XCTAssertEqual(UTType.vouchaOPML.preferredFilenameExtension, "opml")
        XCTAssertFalse(types.contains { UTType.plainText.conforms(to: $0) })
    }

    func testReadsAndClassifiesRealCSVOPMLAndXMLFiles() async throws {
        let cases: [(fileExtension: String, contents: String, format: SourceImportFileFormat)] = [
            ("csv", "url\nhttps://example.test/feed.xml", .csv),
            ("opml", #"<?xml version="1.0"?><opml version="2.0"><body/></opml>"#, .opml),
            ("xml", #"<?xml version="1.0"?><opml version="2.0"><body/></opml>"#, .opml)
        ]

        for item in cases {
            let url = FileManager.default.temporaryDirectory
                .appendingPathComponent("valid-import-\(UUID().uuidString).\(item.fileExtension)")
            defer { try? FileManager.default.removeItem(at: url) }
            try Data(item.contents.utf8).write(to: url)

            let (text, format) = try await NativeImportExportFiles.readUTF8(from: url)
            XCTAssertEqual(text, item.contents)
            XCTAssertEqual(format, item.format)
        }
    }

    func testFileLargerThanRequestLimitIsRejectedBeforeReading() async throws {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("oversized-import-\(UUID().uuidString).csv")
        defer { try? FileManager.default.removeItem(at: url) }
        try Data(repeating: 0x61, count: ImportExportViewModel.maximumRequestBytes + 1).write(to: url)

        do {
            _ = try await NativeImportExportFiles.readUTF8(from: url)
            XCTFail("Expected oversized file to be rejected")
        } catch let error as ImportExportValidationError {
            XCTAssertEqual(error, .requestTooLarge)
        }
    }

    func testNativeCSVParserCountsQuotedAndMultilineFeedURLs() throws {
        let csv = """
        title,xmlUrl,notes
        "News, Daily",https://example.test/news,"First line
        second line"
        Empty,,Ignored
        Podcast,https://example.test/podcast,
        """

        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(in: csv, format: .csv),
            ["https://example.test/news", "https://example.test/podcast"]
        )
    }

    func testNativeCSVParserTreatsEmptyAndWhitespaceOnlyFilesAsNoFeedURLs() throws {
        XCTAssertEqual(try NativeSourceImportParser.feedURLs(in: "", format: .csv), [])
        XCTAssertEqual(try NativeSourceImportParser.feedURLs(in: " \t\r\n ", format: .csv), [])
    }

    func testNativeCSVParserSupportsURLListsAndTSVFallback() throws {
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: "https://example.test/one\r\n\r\n https://example.test/two ",
                format: .csv
            ),
            ["https://example.test/one", "https://example.test/two"]
        )
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: "\u{feff}url\r\nhttps://example.test/bom",
                format: .csv
            ),
            ["https://example.test/bom"]
        )
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: "title\txmlurl\nOne\thttps://example.test/one\nEmpty\t",
                format: .csv
            ),
            ["https://example.test/one"]
        )
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(in: "title\tname\nOne\tFeed", format: .csv),
            []
        )
    }

    func testNativeCSVParserSupportsCarriageReturnRecords() throws {
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: "url\rhttps://example.test/one\rhttps://example.test/two",
                format: .csv
            ),
            ["https://example.test/one", "https://example.test/two"]
        )
    }

    func testNativeCSVParserAutoDetectsOneRecordDelimiterAndSkipsWhitespaceRows() throws {
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: "url\rhttps://example.test/one\nhttps://example.test/two",
                format: .csv
            ),
            ["https://example.test/one\nhttps://example.test/two"]
        )
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: "url\nhttps://example.test/one\r\nhttps://example.test/two",
                format: .csv
            ),
            ["https://example.test/one", "https://example.test/two"]
        )
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: "url,title\n   \nhttps://example.test/feed,Feed",
                format: .csv
            ),
            ["https://example.test/feed"]
        )
    }

    func testNativeCSVParserPreservesEmbeddedBOMAtRowLimitBoundary() throws {
        let rows = Array(repeating: "https://example.test/feed", count: 500)
        let embeddedBOMHeader = "\u{feff}\u{feff}url"

        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: ([embeddedBOMHeader] + rows.dropLast()).joined(separator: "\n"),
                format: .csv
            ).count,
            500
        )
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: ([embeddedBOMHeader] + rows).joined(separator: "\n"),
                format: .csv
            ).count,
            501
        )
    }

    func testNativeTSVFallbackCountsInternalWhitespaceValuesAtRowLimitBoundary() throws {
        let whitespaceRows = Array(repeating: "Feed\t   \tArticle", count: 500)
        let header = "title\turl\ttype"
        let valid = "Feed\thttps://example.test/feed\tArticle"

        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: ([header] + whitespaceRows.dropLast() + [valid]).joined(separator: "\n"),
                format: .csv
            ).count,
            500
        )
        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(
                in: ([header] + whitespaceRows + [valid]).joined(separator: "\n"),
                format: .csv
            ).count,
            501
        )
    }

    func testNativeCSVParserRejectsMalformedRecords() {
        for csv in [
            "url\n\"unclosed",
            "url\nbad\"quote",
            "url\n\"https://example.test/feed\"suffix",
            "url\n,",
            "url,title\none"
        ] {
            XCTAssertThrowsError(try NativeSourceImportParser.feedURLs(in: csv, format: .csv))
        }
    }

    func testNativeOPMLParserCountsOnlyOutlinesWithFeedURLs() throws {
        let opml = """
        <opml version="2.0"><body>
          <outline text="Group">
            <outline text="One" xmlUrl="https://example.test/one" />
            <outline text="Page only" htmlUrl="https://example.test" />
            <outline text="Two" XMLURL="https://example.test/two" />
          </outline>
        </body></opml>
        """

        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(in: opml, format: .opml),
            ["https://example.test/one", "https://example.test/two"]
        )
    }

    func testNativeOPMLParserMatchesBackendOutlineExtraction() throws {
        let opml = """
        <!-- <outline xmlUrl="https://example.test/comment" /> -->
        <outline xmlUrl='https://example.test/single-quoted' />
        <outline XMLURL="https://example.test/malformed-tail" not-an-attribute>
        """

        XCTAssertEqual(
            try NativeSourceImportParser.feedURLs(in: opml, format: .opml),
            ["https://example.test/comment", "https://example.test/malformed-tail"]
        )
    }

    func testTopicExportCreatesShareableJSONAndCleanupRemovesIt() async throws {
        let expected = ExportTopic(name: "Travel", slug: "travel", topicType: "interest")
        let service = ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            exportTopics: { TopicExportResponse(results: [expected]) },
            importSources: { _ in throw URLError(.badServerResponse) },
            sourceStatus: { _ in throw URLError(.badServerResponse) },
            exportSources: { _, _ in Data() }
        )
        let viewModel = ImportExportViewModel(route: .topics, service: service)

        await viewModel.prepareExport()
        let url = try XCTUnwrap(viewModel.exportURL)
        XCTAssertTrue(url.lastPathComponent.hasSuffix("-topics.json"))
        let data = try Data(contentsOf: url)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        XCTAssertEqual(try decoder.decode([ExportTopic].self, from: data), [expected])
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [[String: Any]])
        XCTAssertEqual(object.first?["topic_type"] as? String, "interest")
        XCTAssertNil(object.first?["topicType"])

        viewModel.cancelOperations()
        XCTAssertNil(viewModel.exportURL)
        XCTAssertFalse(FileManager.default.fileExists(atPath: url.path))
    }

    func testFailedExportRemovesStaleShareArtifact() async throws {
        let staleURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("stale-export-\(UUID().uuidString).json")
        try Data("stale".utf8).write(to: staleURL)
        let service = ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            exportTopics: { throw URLError(.badServerResponse) },
            importSources: { _ in throw URLError(.badServerResponse) },
            sourceStatus: { _ in throw URLError(.badServerResponse) },
            exportSources: { _, _ in Data() }
        )
        let viewModel = ImportExportViewModel(route: .topics, service: service)
        viewModel.exportURL = staleURL

        await viewModel.prepareExport()

        XCTAssertNil(viewModel.exportURL)
        XCTAssertFalse(FileManager.default.fileExists(atPath: staleURL.path))
        XCTAssertNotNil(viewModel.errorMessage)
    }
}
