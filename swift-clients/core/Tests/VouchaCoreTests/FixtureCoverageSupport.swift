import Foundation
import XCTest

/// Walks a JSON document and collects every object-property key path it contains, e.g.
/// "community.slug" or "posts.p1.author_id" (dynamic entity-map keys are ordinary object
/// properties, so they appear in the path literally, identically on both sides of a diff).
/// Used to diff a fixture body against the same body decoded into a DTO and re-encoded, so a
/// DTO that silently drops a field surfaces as a path present on one side and missing on the
/// other. Array items are walked without an index suffix — every item in "results" shares the
/// "results.<field>" prefix — so field-level gaps are still caught without depending on item
/// count or order lining up between the two sides; only named object properties count as
/// "fields" a DTO can drop. Mirrors the .NET `JsonKeyPaths` implementation exactly.
enum JsonKeyPaths {
    static func extract(_ data: Data) -> Set<String> {
        guard let root = try? JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed]) else {
            return []
        }
        var paths = Set<String>()
        walk(root, prefix: "", paths: &paths)
        return paths
    }

    private static func walk(_ value: Any, prefix: String, paths: inout Set<String>) {
        switch value {
        case let object as [String: Any]:
            for (key, propertyValue) in object {
                let path = prefix.isEmpty ? key : "\(prefix).\(key)"
                paths.insert(path)
                walk(propertyValue, prefix: path, paths: &paths)
            }
        case let array as [Any]:
            for item in array {
                walk(item, prefix: prefix, paths: &paths)
            }
        default:
            break
        }
    }

}

/// Decodes `fixtureId` into `T`, re-encodes it, and asserts every key path present in the
/// original fixture is still present after the round trip — a DTO that silently drops a field
/// fails this before code review (#6773). `ignoring` documents known, accepted gaps and is
/// unconditional; callers must keep each ignored path precise.
func assertFixtureCoversDTO<T: Codable>(
    _ fixtureId: String,
    as _: T.Type,
    ignoring: Set<String> = [],
    file: StaticString = #filePath,
    line: UInt = #line
) throws {
    let original = ApiFixtureLoader.data(fixtureId, file: file, line: line)
    let dto = try makeVouchaDecoder().decode(T.self, from: original)

    let encoder = JSONEncoder()
    encoder.keyEncodingStrategy = .convertToSnakeCase
    let reEncoded = try encoder.encode(dto)

    var missing = JsonKeyPaths.extract(original)
    missing.subtract(JsonKeyPaths.extract(reEncoded))
    missing.subtract(ignoring)

    XCTAssertTrue(
        missing.isEmpty,
        "\(fixtureId) (\(T.self)) is missing fields the DTO drops on decode: " +
            missing.sorted().joined(separator: ", "),
        file: file,
        line: line
    )
}
