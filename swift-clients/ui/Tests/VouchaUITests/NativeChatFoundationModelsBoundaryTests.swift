import Foundation
@testable import VouchaFeatures
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsBoundaryTests: XCTestCase {
        func testSchemaProjectionAcceptsNullableChoiceAndStringConstant() throws {
            let schema = Data(
                #"{"type":"object","properties":{"choice":{"enum":["one",null]},"kind":{"const":"fixed"}}}"#.utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(schema))
        }

        func testSchemaProjectionRejectsNonObjectAndUnsupportedProperty() {
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data("[]".utf8)))
            let unsupported = Data(#"{"type":"object","properties":{"choice":{"pattern":"x"}}}"#.utf8)
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(unsupported))
        }

        func testSchemaProjectionRejectsNonStringConstantsAndUnprojectableMaps() throws {
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(#"{"const":true}"#.utf8)))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(#"{"const":5}"#.utf8)))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(
                #"{"type":"object","additionalProperties":{"type":"string"}}"#.utf8
            )))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(Data(
                #"{"type":"object","properties":{"id":{"type":"string"}},"additionalProperties":false}"#.utf8
            )))
        }

        func testSchemaProjectionRejectsImplicitOpenMapRequiredForMinimumProperties() {
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(
                #"{"type":"object","minProperties":1}"#.utf8
            )))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(
                ##"{"type":"object","$defs":{"Map":{"type":"object","properties":{"id":{"type":"string"}},"minProperties":2}},"properties":{"value":{"$ref":"#/$defs/Map"}}}"##
                    .utf8
            )))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(Data(
                #"{"type":"object","properties":{"id":{"type":"string"}},"minProperties":1}"#.utf8
            )))
        }

        func testSchemaProjectionKeepsObjectDefinitionsAndBoundedArrays() {
            let schema = Data(
                ##"{"type":"object","$defs":{"Payload":{"type":"object","properties":{"name":{"type":"string"}},"required":["name"],"additionalProperties":false}},"properties":{"payload":{"$ref":"#/$defs/Payload"},"values":{"type":"array","items":{"type":"integer"},"minItems":1,"maxItems":3}},"required":["payload"]}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(schema))
        }

        func testOversizedToolResultKeepsErrorStatusAndBoundedPreview() throws {
            let small = #"{"content":[],"isError":false}"#
            XCTAssertEqual(NativeChatFoundationModelsTool.contextBoundResult(small), small)

            let raw = #"{"content":[{"type":"text","text":""# + String(repeating: "x", count: 100_000)
                + #""}],"isError":true}"#
            let bounded = NativeChatFoundationModelsTool.contextBoundResult(raw)
            XCTAssertLessThanOrEqual(bounded.utf8.count, NativeChatFoundationModelsTool.maximumModelResultBytes)
            let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(bounded.utf8)) as? [String: Any])
            XCTAssertEqual(envelope["isError"] as? Bool, true)
            XCTAssertEqual(envelope["truncated"] as? Bool, true)
            let content = try XCTUnwrap(envelope["content"] as? [[String: String]])
            let preview = try XCTUnwrap(content.first?["text"])
            XCTAssertTrue(preview.hasPrefix("<external-content>"))
            XCTAssertLessThan(preview.count, 1_000)
        }

        func testGeneratedArgumentsRejectNonObjectAndUnsafeInteger() throws {
            let array = try GeneratedContent(json: "[]")
            XCTAssertThrowsError(try NativeChatFoundationModelsTool.jsonArguments(array))

            let unsafeInteger = try GeneratedContent(json: #"{"count":9007199254740992}"#)
            XCTAssertThrowsError(try NativeChatFoundationModelsTool.jsonArguments(unsafeInteger))
        }
    }
#endif
