import Foundation
import XCTest

enum LaunchSmokeFixtures {
    static func manifest() throws -> (root: URL, fixtures: [[String: Any]]) {
        let path = try XCTUnwrap(ProcessInfo.processInfo.environment["VOUCHA_FILAMENTS_CONTRACT_ROOT"])
        _ = try XCTUnwrap(path.hasPrefix("/") && !path.contains("$(") ? path : nil)
        let root = URL(fileURLWithPath: path, isDirectory: true)
            .appendingPathComponent("api-fixtures/v1", isDirectory: true)
        let manifest = try XCTUnwrap(JSONSerialization.jsonObject(
            with: Data(contentsOf: root.appendingPathComponent("manifest.json"))
        ) as? [String: Any])
        let fixtures = try XCTUnwrap(manifest["fixtures"] as? [[String: Any]])
        return (root, fixtures)
    }

    static func fixtureData(
        id: String,
        path: String,
        query: [String: String],
        root: URL,
        fixtures: [[String: Any]]
    ) throws -> Data {
        let fixture = try XCTUnwrap(fixtures.first { $0["id"] as? String == id })
        XCTAssertEqual(fixture["method"] as? String, "GET")
        XCTAssertEqual(fixture["path"] as? String, path)
        XCTAssertEqual(fixture["query"] as? [String: String] ?? [:], query)
        XCTAssertEqual(fixture["auth"] as? String, "fixture-user")
        XCTAssertEqual(fixture["status"] as? Int, 200)
        let bodyFile = try XCTUnwrap(fixture["bodyFile"] as? String)
        _ = try XCTUnwrap(bodyFile.hasPrefix("responses/") && !bodyFile.contains("..") ? bodyFile : nil)
        return try Data(contentsOf: root.appendingPathComponent(bodyFile))
    }
}

#if os(macOS)
    enum LaunchSmokeSidebar {
        static func click(_ label: String, in app: XCUIApplication) {
            let row = app.outlines.staticTexts.matching(displayedText(label)).firstMatch
            XCTAssertTrue(row.waitForExistence(timeout: 20), "Missing sidebar row \(label)")
            if row.isHittable {
                row.click()
            } else {
                row.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5)).click()
            }
        }

        static func visibleText(_ value: String, in app: XCUIApplication) -> XCUIElement {
            app.staticTexts.matching(displayedText(value)).firstMatch
        }

        private static func displayedText(_ value: String) -> NSPredicate {
            NSPredicate(format: "value == %@ OR label == %@", value, value)
        }
    }
#endif
