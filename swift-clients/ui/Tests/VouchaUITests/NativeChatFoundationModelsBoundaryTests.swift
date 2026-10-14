import Foundation
@testable import VouchaFeatures
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsBoundaryTests: XCTestCase {
        func testSchemaProjectionAcceptsNullableChoiceAndNumericConstant() throws {
            let schema = Data(
                #"{"type":"object","properties":{"choice":{"enum":["one",null]},"count":{"const":5}}}"#.utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(schema))
        }

        func testSchemaProjectionRejectsNonObjectAndUnsupportedProperty() {
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data("[]".utf8)))
            let unsupported = Data(#"{"type":"object","properties":{"choice":{"pattern":"x"}}}"#.utf8)
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(unsupported))
        }

        func testSchemaProjectionInfersBooleanConstAndRejectsUnprojectableMaps() throws {
            let boolean = try XCTUnwrap(JSONSerialization
                .jsonObject(with: Data(#"{"const":true}"#.utf8)) as? [String: Any])
            let number = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(#"{"const":5}"#.utf8)) as? [String: Any])
            XCTAssertEqual(NativeChatFoundationModelsSchema.inferredType(boolean), "boolean")
            XCTAssertEqual(NativeChatFoundationModelsSchema.inferredType(number), "number")
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(Data(#"{"const":true}"#.utf8)))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(
                #"{"type":"object","additionalProperties":{"type":"string"}}"#.utf8
            )))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(Data(
                #"{"type":"object","properties":{"id":{"type":"string"}},"additionalProperties":false}"#.utf8
            )))
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
