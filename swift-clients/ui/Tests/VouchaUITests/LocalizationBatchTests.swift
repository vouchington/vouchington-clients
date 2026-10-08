@testable import VouchaLocalization
import XCTest

final class LocalizationBatchTests: XCTestCase {
    func testFlattenedValuesExpandStringPluralAndSelectLeaves() throws {
        let json = """
        {
          "contract": "v1",
          "revision": "rev-1",
          "ttlSeconds": 300,
          "messages": {
            "common.cancel": "Abort",
            "shared.count": {
              "kind": "plural",
              "valueParameter": "count",
              "forms": { "one": "{count} item", "other": "{count} items" }
            },
            "shared.label": {
              "kind": "select-plural",
              "valueParameter": "count",
              "selectParameter": "kind",
              "cases": {
                "post": { "one": "{count} post", "other": "{count} posts" }
              }
            }
          }
        }
        """.data(using: .utf8)!
        let decoder = JSONDecoder()
        let batch = try decoder.decode(LocalizationBatch.self, from: json)
        XCTAssertEqual(batch.contract, "v1")
        XCTAssertEqual(batch.ttlSeconds, 300)
        let values = batch.flattenedValues()
        XCTAssertEqual(values["common.cancel"], "Abort")
        XCTAssertEqual(values["shared.count.__plural.one"], "{count} item")
        XCTAssertEqual(values["shared.count.__plural.other"], "{count} items")
        XCTAssertEqual(values["shared.label.__select.post.one"], "{count} post")
        XCTAssertEqual(values["shared.label.__select.post.other"], "{count} posts")
    }
}
