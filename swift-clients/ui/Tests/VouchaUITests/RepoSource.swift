import Foundation
import XCTest

func repoSource(_ relative: String, filePath: StaticString = #filePath) throws -> String {
    let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
    var roots = [current, URL(fileURLWithPath: "\(filePath)").deletingLastPathComponent()]
    for _ in 0 ..< 16 {
        roots.append(roots[roots.count - 1].deletingLastPathComponent())
    }
    for root in roots {
        let candidate = root.appendingPathComponent(relative)
        if FileManager.default.fileExists(atPath: candidate.path) {
            return try String(contentsOf: candidate, encoding: .utf8)
        }
    }
    throw XCTSkip("Could not find \(relative)")
}
