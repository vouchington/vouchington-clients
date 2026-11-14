import Foundation
import VouchaModels
import XCTest

final class DecodedJSONExactCodecTests: XCTestCase {
    func testExactObjectIndexTracksEquivalentUnicodeNamesThroughMutationAndRemoval() {
        let composed = "é"
        let decomposed = "e\u{301}"
        var object: DecodedJSONObject = [composed: .integer(1), decomposed: .integer(2)]

        XCTAssertEqual(integer(object[composed]), 1)
        XCTAssertEqual(integer(object[decomposed]), 2)
        object[composed] = .integer(3)
        XCTAssertEqual(integer(object[composed]), 3)
        XCTAssertEqual(integer(object[decomposed]), 2)
        XCTAssertEqual(integer(object.removeValue(forKey: composed)), 3)
        XCTAssertNil(object[composed])
        XCTAssertEqual(integer(object[decomposed]), 2)
        XCTAssertEqual(object.count, 1)
    }

    func testCodecRejectsMalformedAndDecodedDuplicateNames() throws {
        for json in [
            #"{"value":"unterminated}"#,
            #"{"value":"bad\qescape"}"#,
            #"{"value":1"#,
            #"{"value":1} trailing"#,
            #"{"😀":1,"\ud83d\ude00":2}"#
        ] {
            XCTAssertThrowsError(
                try DecodedJSONValue(exactJSONData: Data(json.utf8)),
                "Expected rejection for: \(json)"
            )
        }
    }

    func testEscapedQuotesStayWithinTheirStringToken() throws {
        let raw = #"{"text":"a quote: \" and a slash: \\"}"#
        let decoded = try DecodedJSONValue(exactJSONData: Data(raw.utf8))
        guard case let .object(fields) = decoded, case let .string(text)? = fields["text"] else {
            return XCTFail("Expected decoded object string")
        }
        XCTAssertEqual(text, "a quote: \" and a slash: \\")
        let roundTrip = try DecodedJSONValue(exactJSONData: decoded.exactJSONData())
        guard case let .object(roundTripFields) = roundTrip,
              case let .string(roundTripText)? = roundTripFields["text"] else {
            return XCTFail("Expected round trip object string")
        }
        XCTAssertEqual(roundTripText, text)
    }

    func testCodecEnforcesNestingBoundaryBeforeFoundationValidation() throws {
        let accepted = String(repeating: "[", count: 64) + "0" + String(repeating: "]", count: 64)
        let rejected = String(repeating: "[", count: 65) + "0" + String(repeating: "]", count: 65)

        XCTAssertNoThrow(try DecodedJSONValue(exactJSONData: Data(accepted.utf8)))
        XCTAssertThrowsError(try DecodedJSONValue(exactJSONData: Data(rejected.utf8)))
    }

    func testExactCodecBoundsInputAndOutputBytes() throws {
        let oversized = Data(repeating: 120, count: 1_100_001)
        XCTAssertThrowsError(try DecodedJSONValue(exactJSONData: oversized))
        XCTAssertThrowsError(try DecodedJSONValue.string(String(repeating: "x", count: 1_100_000)).exactJSONData())
    }

    func testScalarCodecKeepsExistingDecodedNumberRepresentations() throws {
        let cases: [(String, (DecodedJSONValue) -> Bool)] = [
            (#"1.25e2"#, { if case .number(125) = $0 { true } else { false } }),
            (#"9007199254740993"#, { if case .integer(9_007_199_254_740_993) = $0 { true } else { false } }),
            (#"18446744073709551616"#, { if case .decimal = $0 { true } else { false } }),
            (#""text""#, { if case .string("text") = $0 { true } else { false } }),
            (#"true"#, { if case .bool(true) = $0 { true } else { false } }),
            (#"null"#, { if case .null = $0 { true } else { false } })
        ]

        for (json, matchesExpected) in cases {
            let decoded = try DecodedJSONValue(exactJSONData: Data(json.utf8))
            XCTAssertTrue(matchesExpected(decoded), "Unexpected decoded scalar for \(json)")
            let encoded = try decoded.exactJSONData()
            XCTAssertTrue(try matchesExpected(DecodedJSONValue(exactJSONData: encoded)))
        }
    }

    func testStringKeyedCodableConversionsRejectCanonicalCollisions() throws {
        let object = DecodedJSONObject(members: [
            ("é", .integer(1)),
            ("e\u{301}", .integer(2))
        ])

        XCTAssertThrowsError(try object.mapValues { $0 })
        XCTAssertThrowsError(try JSONEncoder().encode(DecodedJSONValue.object(object)))
    }

    func testOpaqueObjectKeysBypassModelKeyStrategies() throws {
        let input = Data(#"{"contentSnippet":{"nestedKey":1}}"#.utf8)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let decoded = try decoder.decode(DecodedJSONValue.self, from: input)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try encoder.encode(decoded)
        let exact = try DecodedJSONValue(exactJSONData: encoded)

        guard case let .object(fields) = exact,
              case let .object(nested)? = fields["contentSnippet"] else {
            return XCTFail("Expected original opaque object member names")
        }
        XCTAssertNotNil(nested["nestedKey"])
        XCTAssertNil(fields["content_snippet"])
    }

    private func integer(_ value: DecodedJSONValue?) -> Int64? {
        guard case let .integer(number)? = value else { return nil }
        return number
    }
}
