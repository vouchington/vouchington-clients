import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsReviewBoundaryTests: XCTestCase {
        func testFixedSafeDecimalUsesSerializedGuideWidthForAdmission() throws {
            let schema = Data(
                #"{"type":"object","required":["rows"],"properties":{"rows":{"type":"array","minItems":3,"maxItems":3,"items":{"type":"array","minItems":64,"maxItems":64,"items":{"type":"number","minimum":9007199254740991,"maximum":9007199254740991}}}}}"#
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
            let row = DecodedJSONValue.array(Array(repeating: .number(9_007_199_254_740_991), count: 64))
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["rows": .array(Array(repeating: row, count: 3))]), schema: authoritative
            ))
            let serialized = try JSONSerialization.data(
                withJSONObject: [Double(9_007_199_254_740_991)]
            )
            XCTAssertGreaterThan(serialized.count - 2, 4)
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(schema))
        }

        func testOptionalLiteralShapeMustFitArgumentAdmissionBudget() throws {
            let literal = String(repeating: "x", count: 350)
            let properties = (1 ... 4).map { index in
                #""value\#(index)":{"type":"string","const":"\#(literal)"}"#
            }.joined(separator: ",")
            let schema = Data(#"{"type":"object","properties":{\#(properties)}}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([:]), schema: authoritative))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(schema))

            let small = Data(
                #"{"type":"object","properties":{"first":{"type":"string","const":"\#(literal)"}}}"#.utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(small))
        }

        func testMinimumArgumentBytesMustFitSessionAdmissionBudget() throws {
            let large = String(repeating: "x", count: 700)
            let schema = Data(
                #"{"type":"object","required":["values"],"properties":{"values":{"type":"array","minItems":2,"maxItems":2,"items":{"type":"string","const":"\#(large)"}}}}"#
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["values": .array([.string(large), .string(large)])]), schema: authoritative
            ))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(schema))

            let small = Data(
                #"{"type":"object","properties":{"values":{"type":"array","minItems":2,"maxItems":2,"items":{"type":"string","const":"ok"}}}}"#
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(small))
        }

        func testUnequalLargeIntegerRangeStillConsumesArgumentBytes() throws {
            let schema = Data(
                #"{"type":"object","required":["rows"],"properties":{"rows":{"type":"array","minItems":4,"maxItems":4,"items":{"type":"array","minItems":20,"maxItems":20,"items":{"type":"integer","minimum":9000000000000000,"maximum":9000000000000100}}}}}"#
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
            let row = DecodedJSONValue.array(Array(repeating: .integer(9_000_000_000_000_000), count: 20))
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["rows": .array(Array(repeating: row, count: 4))]), schema: authoritative
            ))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(schema))
        }

        func testWideDecimalRangeHasConservativeMinimumJsonWidth() throws {
            let schema = Data(
                #"{"type":"object","required":["rows"],"properties":{"rows":{"type":"array","minItems":4,"maxItems":4,"items":{"type":"array","minItems":60,"maxItems":60,"items":{"type":"number","minimum":9000000000000000,"maximum":9000000000000100}}}}}"#
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
            let row = DecodedJSONValue.array(Array(repeating: .number(9_000_000_000_000_000), count: 60))
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["rows": .array(Array(repeating: row, count: 4))]), schema: authoritative
            ))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(schema))
        }

        func testUntypedNullConstantProjectsWithoutWidening() throws {
            let property = Data(#"{"const":null}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: property)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.null, schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("other"), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(property))
        }

        func testUniqueArrayWithAtMostOneElementRemainsProjectable() throws {
            let schema = Data(#"{"type":"array","items":{"type":"string"},"uniqueItems":true,"maxItems":1}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.array([.string("one")]), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
                .array([.string("one"), .string("one")]), schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(schema))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(Data(
                #"{"type":"array","items":{"type":"string"},"uniqueItems":true,"maxItems":2}"#.utf8
            )))
        }

        func testReachableOpenObjectDefinitionIsProjectable() throws {
            let schema = Data(
                ##"{"type":"object","$defs":{"Payload":{"type":"object","properties":{"id":{"type":"string"}},"additionalProperties":true}},"properties":{"payload":{"$ref":"#/$defs/Payload"}}}"##
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["payload": .object(["id": .string("a")])]), schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(schema))
        }

        func testTypedNullConstantIsProjectable() throws {
            let property = Data(#"{"type":"null","const":null}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: property)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.null, schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("no"), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(property))
        }

        func testZeroMaximumPropertiesSkipsUnreachableInvalidOptionalSchema() throws {
            let property = Data(
                ##"{"type":"object","maxProperties":0,"properties":{"unused":{"$ref":"#/$defs/Missing"}}}"##.utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: property)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([:]), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(property))
            let root = Data(
                ##"{"type":"object","$defs":{"Empty":{"type":"object","maxProperties":0,"properties":{"unused":{"pattern":"["}}}},"properties":{"value":{"$ref":"#/$defs/Empty"}}}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(root))
        }

        func testForcedNumericArraysExceedOutputByteBudget() throws {
            let large = Data(
                #"{"type":"object","required":["rows"],"properties":{"rows":{"type":"array","minItems":4,"maxItems":4,"items":{"type":"array","minItems":62,"maxItems":62,"items":{"type":"integer","minimum":9007199254740991,"maximum":9007199254740991}}}}}"#
                    .utf8
            )
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(large))
            let small = Data(
                #"{"type":"object","properties":{"count":{"type":"integer","minimum":10,"maximum":10}}}"#.utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(small))
        }
    }
#endif
