import Crypto
import Foundation
@testable import VouchaAPI
import XCTest

final class CanonicalRequestStringTests: XCTestCase {
    func testBuildProducesExactCanonicalString() {
        let result = CanonicalRequestString.build(
            method: "get",
            path: "/api/v1/posts",
            bodyHash: "deadbeef",
            timestamp: 1_700_000_000,
            nonce: "my-nonce"
        )
        XCTAssertEqual(result, "VOUCHA-REQSIG-v1\nGET\n/api/v1/posts\ndeadbeef\n1700000000\nmy-nonce")
    }

    func testBuildUppercasesMethod() {
        let result = CanonicalRequestString.build(
            method: "post",
            path: "/v1/x",
            bodyHash: "bh",
            timestamp: 0,
            nonce: "n"
        )
        XCTAssertTrue(result.hasPrefix("VOUCHA-REQSIG-v1\nPOST\n"))
    }

    func testBuildHasNoTrailingNewline() {
        let result = CanonicalRequestString.build(
            method: "GET",
            path: "/api/v1/posts",
            bodyHash: CanonicalRequestString.emptyBodyHash,
            timestamp: 1_000,
            nonce: "abc"
        )
        XCTAssertFalse(result.hasSuffix("\n"), "canonical string must not end with a newline")
    }

    func testBuildHasFiveNewlineSeparators() {
        let result = CanonicalRequestString.build(
            method: "GET",
            path: "/v1/p",
            bodyHash: "bh",
            timestamp: 1,
            nonce: "n"
        )
        XCTAssertEqual(result.filter { $0 == "\n" }.count, 5)
    }

    func testBodyHashReturnsEmptyHashForNilData() {
        XCTAssertEqual(
            CanonicalRequestString.bodyHash(from: nil),
            CanonicalRequestString.emptyBodyHash
        )
    }

    func testBodyHashReturnsEmptyHashForEmptyData() {
        XCTAssertEqual(
            CanonicalRequestString.bodyHash(from: Data()),
            CanonicalRequestString.emptyBodyHash
        )
    }

    func testEmptyBodyHashConstantMatchesCryptoKit() {
        let expected = SHA256.hash(data: Data())
            .compactMap { String(format: "%02x", $0) }.joined()
        XCTAssertEqual(CanonicalRequestString.emptyBodyHash, expected)
    }

    func testBodyHashMatchesCryptoKitForNonEmptyData() {
        let data = Data("hello".utf8)
        let expected = SHA256.hash(data: data)
            .compactMap { String(format: "%02x", $0) }.joined()
        XCTAssertEqual(CanonicalRequestString.bodyHash(from: data), expected)
    }

    func testBodyHashIsLowercaseHex() {
        let result = CanonicalRequestString.bodyHash(from: Data("test".utf8))
        XCTAssertTrue(result.allSatisfy { $0.isHexDigit && !$0.isUppercase })
    }
}
