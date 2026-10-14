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

        func testGeneratedArgumentsRejectNonObjectAndUnsafeInteger() throws {
            let array = try GeneratedContent(json: "[]")
            XCTAssertThrowsError(try NativeChatFoundationModelsTool.jsonArguments(array))

            let unsafeInteger = try GeneratedContent(json: #"{"count":9007199254740992}"#)
            XCTAssertThrowsError(try NativeChatFoundationModelsTool.jsonArguments(unsafeInteger))
        }
    }
#endif
