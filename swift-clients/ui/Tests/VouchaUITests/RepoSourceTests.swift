import Foundation
import XCTest

final class RepoSourceTests: XCTestCase {
    func testReadsTheUIPackageManifest() throws {
        let source = try repoSource("swift-clients/ui/Package.swift")
        XCTAssertTrue(source.contains("name: \"VouchaUI\""))
    }

    func testReadsAFileCreatedInTheWorkingDirectory() throws {
        let relative = "repo-source-\(UUID().uuidString).txt"
        let url = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent(relative)
        try "working-directory-marker".write(to: url, atomically: true, encoding: .utf8)
        defer { try? FileManager.default.removeItem(at: url) }

        let source = try repoSource(relative, filePath: "/tmp/voucha-repo-source-unrelated.swift")
        XCTAssertEqual(source, "working-directory-marker")
    }

    func testMissingRepositoryFileSkips() {
        let relative = "swift-clients/ui/missing-repo-source-\(UUID().uuidString).swift"
        XCTAssertThrowsError(try repoSource(relative)) { error in
            XCTAssertEqual((error as? XCTSkip)?.message, "Could not find \(relative)")
        }
    }
}
