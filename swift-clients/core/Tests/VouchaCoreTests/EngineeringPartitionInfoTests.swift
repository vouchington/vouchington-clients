import Foundation
@testable import VouchaAPI
import XCTest

final class EngineeringPartitionInfoTests: XCTestCase {
    func testDropRefusalWireVariantsRoundTrip() throws {
        let cases: [(String, EngineeringPartitionDropRefusal?)] = [
            (#""check-pending""#, .checkPending),
            (#""in-flight""#, .inFlight),
            (#""inside-retention""#, .insideRetention),
            (#""legal-hold""#, .legalHold),
            (#""no-declared-retention""#, .noDeclaredRetention),
            ("null", nil)
        ]
        for (wire, expected) in cases {
            let json = #"{"name":"posts_1","size_bytes":256,"drop_refusal":\#(wire)}"#
            let partition = try makeVouchaDecoder().decode(EngineeringPartitionInfo.self, from: Data(json.utf8))
            XCTAssertEqual(partition.name, "posts_1")
            XCTAssertEqual(partition.sizeBytes, 256)
            XCTAssertEqual(partition.dropRefusal, expected)
            let encoder = JSONEncoder()
            encoder.keyEncodingStrategy = .convertToSnakeCase
            let encoded = try encoder.encode(partition)
            let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
            if let expected {
                XCTAssertEqual(object["drop_refusal"] as? String, expected.rawValue)
            } else {
                XCTAssertTrue(object["drop_refusal"] is NSNull)
            }
        }
    }

    func testDropRefusalRequiresARecognizedValueOrExplicitNull() {
        for json in [
            #"{"name":"posts_1","size_bytes":256}"#,
            #"{"name":"posts_1","size_bytes":256,"drop_refusal":"unknown"}"#,
            #"{"name":"posts_1","size_bytes":256,"drop_refusal":"CHECK-PENDING"}"#,
            #"{"name":"posts_1","size_bytes":256,"drop_refusal":3}"#
        ] {
            XCTAssertThrowsError(try makeVouchaDecoder().decode(EngineeringPartitionInfo.self, from: Data(json.utf8)))
        }
    }
}
