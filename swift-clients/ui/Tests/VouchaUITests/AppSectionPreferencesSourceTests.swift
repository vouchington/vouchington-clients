import Foundation
import XCTest

final class AppSectionPreferencesSourceTests: XCTestCase {
    func testNavigationCustomizationWiresVisibilityReorderingAndPersistence() throws {
        let preferences = try source("AppSectionPreferences.swift")
        let editor = try source("BottomBarEditView.swift")

        XCTAssertTrue(preferences.contains("guard visibleSections.count > 1 else { return }"))
        XCTAssertTrue(preferences.contains("hiddenSectionIDs.insert(section.rawValue)"))
        XCTAssertTrue(preferences.contains("sections.move(fromOffsets: source, toOffset: destination)"))
        XCTAssertTrue(preferences.contains("try? data.write(to: url, options: .atomic)"))
        XCTAssertTrue(editor.contains("preferences.move(fromOffsets: source, toOffset: destination)"))
        XCTAssertTrue(editor.contains("preferences.setHidden(section, hidden: !newValue)"))
        XCTAssertTrue(editor.contains("onSave(normalized)"))
    }

    private func source(_ file: String, filePath: StaticString = #filePath) throws -> String {
        let relative = "swift-clients/apps/shared-app/\(file)"
        for root in repoRootCandidates(startingFrom: filePath) {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
        }
        throw XCTSkip("Could not find \(relative) from the test working directory")
    }

    private func repoRootCandidates(startingFrom file: StaticString) -> [URL] {
        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let source = URL(fileURLWithPath: "\(file)", relativeTo: current).standardizedFileURL
        return [current] + ancestors(of: current) + [source.deletingLastPathComponent()]
            + ancestors(of: source.deletingLastPathComponent())
    }

    private func ancestors(of url: URL) -> [URL] {
        var directory = url
        return (0 ..< 16).map { _ in
            directory.deleteLastPathComponent()
            return directory
        }
    }
}
