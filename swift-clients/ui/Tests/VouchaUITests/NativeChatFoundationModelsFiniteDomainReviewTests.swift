import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsFiniteDomainReviewTests: XCTestCase {
        func testUntypedIntegerConstantIgnoresStringLength() throws {
            let raw = Data(#"{"const":7,"minLength":3}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.integer(7), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.integer(8), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(raw))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(Data(
                #"{"const":7,"minimum":8}"#.utf8
            )))
        }

        func testFiniteUntypedStringAndNullEnumsIgnoreNumericMinimum() throws {
            for (raw, accepted) in [
                (#"{"enum":["draft","published"],"minimum":1}"#, DecodedJSONValue.string("draft")),
                (#"{"enum":[null],"minimum":1}"#, DecodedJSONValue.null),
                (#"{"enum":["draft",null],"minimum":1}"#, DecodedJSONValue.null)
            ] {
                let data = Data(raw.utf8)
                let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: data)
                XCTAssertTrue(try NativeChatMCPInputSchema.accepts(accepted, schema: authoritative))
                XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(data))
            }
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(Data(
                #"{"enum":["draft",7],"minimum":1}"#.utf8
            )))
        }

        func testReferencedStringConstantIntersectsEnum() throws {
            let raw = Data(
                ##"{"type":"object","$defs":{"Mode":{"type":"string","const":"draft","enum":["draft","published"]}},"properties":{"mode":{"$ref":"#/$defs/Mode"}}}"##
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["mode": .string("draft")]), schema: authoritative
            ))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
                .object(["mode": .string("published")]), schema: authoritative
            ))
            let projected = try NativeChatFoundationModelsSchema.project(raw)
            let encoded = try XCTUnwrap(JSONSerialization
                .jsonObject(with: JSONEncoder().encode(projected)) as? [String: Any])
            let definitions = try XCTUnwrap(encoded["$defs"] as? [String: [String: Any]])
            let mode = try XCTUnwrap(definitions["Mode"])
            let alternatives = try XCTUnwrap(mode["anyOf"] as? [[String: Any]])
            XCTAssertEqual(alternatives.count, 1)
            XCTAssertEqual(alternatives[0]["enum"] as? [String], ["draft"])
        }

        func testUniqueSingletonItemCapsProjectedArrayAtOne() throws {
            let raw = Data(#"{"type":"array","items":{"const":"x"},"minItems":1,"maxItems":5,"uniqueItems":true}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.array([.string("x")]), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
                .array([.string("x"), .string("x")]), schema: authoritative
            ))
            let projected = try NativeChatFoundationModelsSchema.projectWithProperty(raw)
            let encoded = try XCTUnwrap(JSONSerialization
                .jsonObject(with: JSONEncoder().encode(projected)) as? [String: Any])
            let properties = try XCTUnwrap(encoded["properties"] as? [String: Any])
            let value = try XCTUnwrap(properties["value"] as? [String: Any])
            XCTAssertEqual(value["maxItems"] as? Int, 1)
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(Data(
                #"{"type":"array","items":{"const":"x"},"minItems":2,"uniqueItems":true}"#.utf8
            )))
        }

        func testReachableBooleanDefinitionIgnoresProperties() throws {
            let raw = Data(
                ##"{"type":"object","$defs":{"Flag":{"type":"boolean","properties":{"ignored":{"type":"string"}}}},"properties":{"flag":{"$ref":"#/$defs/Flag"}}}"##
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["flag": .bool(true)]), schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(raw))
        }

        func testUniqueSingletonPreflightUsesProjectedMaximum() throws {
            let literal = String(repeating: "x", count: 40)
            let raw = Data(
                #"{"type":"array","items":{"const":"\#(literal)"},"uniqueItems":true}"#.utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.array([.string(literal)]), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(raw))
        }

        func testUntypedFixedStringDoesNotTraverseIgnoredItems() throws {
            let raw = Data(##"{"const":"fixed","items":{"$ref":"#/$defs/Missing"}}"##.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.string("fixed"), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(raw))
        }

        func testUntypedExactFractionalConstantProjectsAsNumber() throws {
            let raw = Data(#"{"const":1.5,"enum":[1.5,2.5]}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.decimal(1.5), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.decimal(2.5), schema: authoritative))
            let projected = try NativeChatFoundationModelsSchema.projectWithProperty(raw)
            let encoded = try XCTUnwrap(JSONSerialization
                .jsonObject(with: JSONEncoder().encode(projected)) as? [String: Any])
            let properties = try XCTUnwrap(encoded["properties"] as? [String: Any])
            let value = try XCTUnwrap(properties["value"] as? [String: Any])
            XCTAssertEqual((value["minimum"] as? NSNumber)?.decimalValue, Decimal(1.5))
            XCTAssertEqual((value["maximum"] as? NSNumber)?.decimalValue, Decimal(1.5))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(Data(
                #"{"const":1.234567890123456789}"#.utf8
            )))
        }

        func testTypedStringEnumsFilterUnreachableNullBeforeDisjointness() throws {
            let raw = Data(
                #"{"oneOf":[{"type":"string","enum":["draft",null]},{"type":"string","enum":["published",null]}]}"#.utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.string("draft"), schema: authoritative))
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.string("published"), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.null, schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(raw))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(Data(
                #"{"oneOf":[{"type":"string","enum":["draft",null]},{"type":"string","enum":["draft",null]}]}"#.utf8
            )))
        }
    }
#endif
