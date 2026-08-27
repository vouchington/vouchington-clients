@testable import VouchaModels
import XCTest

final class RequiredNullableDecodingTests: XCTestCase {
    private struct Payload: Decodable {
        @RequiredNullable
        var value: String?
    }

    func testDecodesExplicitNull() throws {
        let payload = try JSONDecoder().decode(Payload.self, from: Data(#"{"value":null}"#.utf8))

        XCTAssertNil(payload.value)
    }

    func testDecodesNonNullValue() throws {
        let payload = try JSONDecoder().decode(Payload.self, from: Data(#"{"value":"present"}"#.utf8))

        XCTAssertEqual(payload.value, "present")
    }

    func testRejectsMissingKey() {
        XCTAssertThrowsError(try JSONDecoder().decode(Payload.self, from: Data(#"{}"#.utf8))) { error in
            guard case let DecodingError.keyNotFound(key, _) = error else {
                return XCTFail("Expected keyNotFound, got \(error)")
            }

            XCTAssertEqual(key.stringValue, "value")
        }
    }
}
