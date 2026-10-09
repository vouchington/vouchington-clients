@testable import VouchaCore
import XCTest

final class Base64URLTests: XCTestCase {
    func testEncodesUnpaddedURLAlphabet() {
        XCTAssertEqual(Data().base64URLEncodedString(), "")
        XCTAssertEqual(Data("f".utf8).base64URLEncodedString(), "Zg")
        XCTAssertEqual(Data("foobar".utf8).base64URLEncodedString(), "Zm9vYmFy")
        XCTAssertEqual(Data([0xFB, 0xFF, 0xBF]).base64URLEncodedString(), "-_-_")
    }

    func testDecodesPaddedAndUnpaddedURLAlphabet() throws {
        XCTAssertEqual(try Data(base64URLEncoded: ""), Data())
        XCTAssertEqual(try Data(base64URLEncoded: "Zg"), Data("f".utf8))
        XCTAssertEqual(try Data(base64URLEncoded: "Zg=="), Data("f".utf8))
        XCTAssertEqual(try Data(base64URLEncoded: "Zm9vYmFy"), Data("foobar".utf8))
        XCTAssertEqual(try Data(base64URLEncoded: "-_-_"), Data([0xFB, 0xFF, 0xBF]))
    }

    func testRejectsInvalidBase64URLWithThePasskeyPrecondition() {
        XCTAssertThrowsError(try Data(base64URLEncoded: "@@@@")) { error in
            guard case VouchaError.api(statusCode: 0, preconditionCode: "INVALID_BASE64URL") = error else {
                return XCTFail("unexpected error: \(error)")
            }
        }
    }
}
