import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

final class NativeChatMCPInputSchemaBoundsTests: XCTestCase {
    func testSchemaNumericEqualityAppliesToConstEnumAndUniqueItems() throws {
        let constant: DecodedJSONValue = .object(["const": .integer(1)])
        let choices: DecodedJSONValue = .object(["enum": .array([.unsignedInteger(1)])])
        let unique: DecodedJSONValue = .object([
            "type": .string("array"), "uniqueItems": .bool(true), "maxItems": .integer(256)
        ])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.number(1), schema: constant))
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.decimal(Decimal(1)), schema: choices))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
            .array([.integer(1), .decimal(Decimal(1))]), schema: unique
        ))
    }

    func testUniqueItemsValidationHasAnIndependentWorkBound() throws {
        let unique: DecodedJSONValue = .object([
            "type": .string("array"), "uniqueItems": .bool(true), "maxItems": .integer(256)
        ])
        let values: [DecodedJSONValue] = (0 ... 256).map { .string(String($0)) }
        try NativeChatMCPInputSchema.validateSchema(unique)
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.array(Array(values.prefix(256))), schema: unique))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.array(values), schema: unique))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "type": .string("array"), "uniqueItems": .bool(true)
        ])))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "type": .string("array"), "uniqueItems": .bool(true), "maxItems": .integer(257)
        ])))
    }

    func testComposedReferenceValidationHasACumulativeWorkBound() throws {
        var definitions: [String: DecodedJSONValue] = [
            "leaf": .object(["type": .string("object")])
        ]
        for level in (0 ..< 7).reversed() {
            let next = level == 6 ? "leaf" : "level\(level + 1)"
            definitions["level\(level)"] = .object([
                "allOf": .array(Array(repeating: .object([
                    "$ref": .string("#/$defs/\(next)")
                ]), count: 4))
            ])
        }
        let schema: DecodedJSONValue = .object([
            "$defs": .object(definitions), "$ref": .string("#/$defs/level0")
        ])
        try NativeChatMCPInputSchema.validateSchema(schema)
        XCTAssertThrowsError(try NativeChatMCPInputSchema.accepts(.object([:]), schema: schema))

        let bounded: DecodedJSONValue = .object([
            "$defs": .object(definitions),
            "allOf": .array(Array(repeating: .object(["$ref": .string("#/$defs/leaf")]), count: 2))
        ])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([:]), schema: bounded))
    }

    func testSchemaBoundsKeepLargeIntegerPrecisionAndRejectUnrepresentableCounts() throws {
        let maximum: DecodedJSONValue = .object(["maximum": .integer(9_007_199_254_740_992)])
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
            .integer(9_007_199_254_740_993), schema: maximum
        ))
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
            .integer(9_007_199_254_740_992), schema: maximum
        ))
        let invalidCount: DecodedJSONValue = .object([
            "type": .string("array"), "maxItems": .unsignedInteger(UInt64.max)
        ])
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(invalidCount))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "minLength": .decimal(Decimal(-1))
        ])))
        let integerType: DecodedJSONValue = .object(["type": .string("integer")])
        let largeInteger = try XCTUnwrap(Decimal(string: "18446744073709551615"))
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.decimal(largeInteger), schema: integerType))
    }

    func testInputNumericBoundsRejectValuesOutsideDecimalRange() throws {
        let nonnegative: DecodedJSONValue = .object([
            "type": .string("number"), "minimum": .integer(0)
        ])
        let nonpositive: DecodedJSONValue = .object([
            "type": .string("number"), "maximum": .integer(0)
        ])

        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(1e300), schema: nonpositive))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(-1e300), schema: nonnegative))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(1e-300), schema: nonpositive))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(-1e-300), schema: nonnegative))

        let closedInterval: DecodedJSONValue = .object([
            "type": .string("number"), "minimum": .integer(-2), "maximum": .integer(2)
        ])
        let fraction = try XCTUnwrap(Decimal(string: "1.25"))
        for accepted: DecodedJSONValue in [
            .number(-2), .integer(2), .unsignedInteger(1), .decimal(fraction),
            .number(0)
        ] {
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(accepted, schema: closedInterval))
        }
        let numericKeywordOnly: DecodedJSONValue = .object(["minimum": .integer(0)])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.string("ignored"), schema: numericKeywordOnly))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(.infinity), schema: closedInterval))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(.nan), schema: closedInterval))

        let boundedArray: DecodedJSONValue = .object([
            "type": .string("array"), "items": .object([
                "type": .string("number"), "minimum": .integer(0), "maximum": .unsignedInteger(UInt64.max)
            ])
        ])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
            .array(Array(repeating: .unsignedInteger(UInt64.max), count: 256)), schema: boundedArray
        ))
    }

    func testInputNumericBoundsApplyThroughNestedCompositionAndReference() throws {
        let schema: DecodedJSONValue = .object([
            "$defs": .object(["nonpositive": .object([
                "type": .string("number"), "maximum": .integer(0)
            ])]),
            "type": .string("object"),
            "properties": .object(["samples": .object([
                "type": .string("array"), "items": .object([
                    "allOf": .array([.object(["$ref": .string("#/$defs/nonpositive")])])
                ])
            ])])
        ])

        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([
            "samples": .array([.integer(0), .number(-1e300)])
        ]), schema: schema))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.object([
            "samples": .array([.integer(0), .number(1e300)])
        ]), schema: schema))
    }
}
