import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsReviewBoundaryTests: XCTestCase {
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
