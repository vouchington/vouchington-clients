import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsProjectionBudgetTests: XCTestCase {
        func testDecimalGuideRequiresAtLeastOneJSONSerializableDouble() throws {
            let impossible = Data(#"{"type":"number","minimum":0.100000000000000005,"maximum":0.100000000000000005}"#
                .utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: impossible)
            let exact = try XCTUnwrap(Decimal(string: "0.100000000000000005"))
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.decimal(exact), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(0.1), schema: authoritative))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(impossible))

            let possible = Data(#"{"type":"number","minimum":0.1,"maximum":0.10000000000000001}"#.utf8)
            let projected = try NativeChatFoundationModelsSchema.project(possible)
            let encoded = try XCTUnwrap(String(data: JSONEncoder().encode(projected), encoding: .utf8))
            XCTAssertTrue(encoded.contains("0.1"))
            XCTAssertFalse(encoded.contains("0.100000000000000005"))
        }

        func testArrayMinimumMustFitBoundedModelSession() throws {
            let excessive = Data(#"{"type":"array","items":{"type":"null"},"minItems":10000}"#.utf8)
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(excessive))
            let allowed = Data(#"{"type":"array","items":{"type":"null"},"minItems":64,"maxItems":10000}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: allowed)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .array(Array(repeating: .null, count: 64)),
                schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(allowed))
        }

        func testGeneratedNodeNameSkipsReachableDefinitionName() throws {
            let raw = Data(
                ##"{"type":"object","$defs":{"MCPNode_1":{"type":"string","enum":["ready"]}},"properties":{"value":{"$ref":"#/$defs/MCPNode_1"}}}"##
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["value": .string("ready")]),
                schema: authoritative
            ))
            let projected = try NativeChatFoundationModelsSchema.project(raw)
            let encoded = try XCTUnwrap(String(data: JSONEncoder().encode(projected), encoding: .utf8))
            XCTAssertTrue(encoded.contains("MCPNode_1"))
            XCTAssertTrue(encoded.contains("MCPNode_2"))
        }

        func testNestedRequiredArraysShareOneOutputBudget() throws {
            let excessive = Data(
                #"{"type":"array","minItems":64,"items":{"type":"array","minItems":64,"items":{"type":"null"}}}"#.utf8
            )
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(excessive))

            let bounded = Data(
                #"{"type":"object","required":["rows"],"properties":{"rows":{"type":"array","minItems":4,"items":{"type":"array","minItems":4,"items":{"type":"null"}}}}}"#
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(bounded))

            let referenced = Data(
                ##"{"type":"array","minItems":64,"items":{"$ref":"#/$defs/Row"},"$defs":{"Row":{"type":"array","minItems":64,"items":{"type":"null"}}}}"##
                    .utf8
            )
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(referenced))
        }

        func testNullOnlyEnumAndAnnotationOnlySchemaRemainProjectable() throws {
            let nullOnly = Data(#"{"enum":[null]}"#.utf8)
            let nullSchema = try JSONDecoder().decode(DecodedJSONValue.self, from: nullOnly)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.null, schema: nullSchema))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("other"), schema: nullSchema))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(nullOnly))

            let annotationOnly = Data(
                #"{"description":"Arbitrary JSON payload","default":{"ok":true},"examples":[null,{"ok":true}]}"#.utf8
            )
            let arbitrarySchema = try JSONDecoder().decode(DecodedJSONValue.self, from: annotationOnly)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object(["ok": .bool(true)]), schema: arbitrarySchema))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(annotationOnly))

            let nested = Data(
                ##"{"type":"object","properties":{"choice":{"$ref":"#/$defs/NullChoice"},"payload":{"description":"Any JSON"}},"$defs":{"NullChoice":{"enum":[null]}}}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(nested))

            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(#"{"minimum":1}"#.utf8)))
        }
    }
#endif
