import Foundation
import XCTest

final class JsonKeyPathsTests: XCTestCase {
    func testExtractsFlatObjectPaths() {
        let json = Data(#"{"id":"a","name":"b"}"#.utf8)
        XCTAssertEqual(JsonKeyPaths.extract(json), Set(["id", "name"]))
    }

    func testExtractsNestedObjectPaths() {
        let json = Data(#"{"community":{"slug":"test","metrics":{"member_count":3}}}"#.utf8)
        XCTAssertEqual(
            JsonKeyPaths.extract(json),
            Set(["community", "community.slug", "community.metrics", "community.metrics.member_count"])
        )
    }

    func testExtractsDynamicEntityMapKeysLiterally() {
        let json = Data(#"{"posts":{"p1":{"author_id":"u1"},"p2":{"author_id":"u2"}}}"#.utf8)
        let paths = JsonKeyPaths.extract(json)
        XCTAssertTrue(paths.contains("posts.p1.author_id"))
        XCTAssertTrue(paths.contains("posts.p2.author_id"))
    }

    func testExtractsArrayItemFieldsWithoutIndexSuffix() {
        let json = Data(#"{"results":[{"id":"a"},{"id":"b","extra":true}]}"#.utf8)
        XCTAssertEqual(JsonKeyPaths.extract(json), Set(["results", "results.id", "results.extra"]))
    }

    func testExtractsArrayOfScalars() {
        let json = Data(#"{"tags":["a","b","c"]}"#.utf8)
        XCTAssertEqual(JsonKeyPaths.extract(json), Set(["tags"]))
    }

    func testScalarAndNullLeavesDoNotRecurseButPropertyPathIsRecorded() {
        let json = Data(#"{"deleted_at":null,"count":1,"name":"x","active":true}"#.utf8)
        XCTAssertEqual(JsonKeyPaths.extract(json), Set(["deleted_at", "count", "name", "active"]))
    }

    func testEmptyObjectAndEmptyArrayProduceNoNestedPaths() {
        let json = Data(#"{"empty_object":{},"empty_array":[]}"#.utf8)
        XCTAssertEqual(JsonKeyPaths.extract(json), Set(["empty_object", "empty_array"]))
    }

    func testTopLevelScalarProducesNoPaths() {
        XCTAssertTrue(JsonKeyPaths.extract(Data("42".utf8)).isEmpty)
    }

    func testTopLevelArrayWalksItemsWithEmptyPrefix() {
        let json = Data(#"[{"id":"a"},{"id":"b"}]"#.utf8)
        XCTAssertEqual(JsonKeyPaths.extract(json), Set(["id"]))
    }

}
