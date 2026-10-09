import Foundation
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class LocalImagePreviewTests: XCTestCase {
    func testDecodeFailureAfterUploadShowsTypedAccessibleMessage() throws {
        let preview = LocalImagePreview(data: Data("not an image".utf8))

        XCTAssertEqual(
            try preview.inspect().find(text: "Image uploaded. Preview unavailable.").string(),
            "Image uploaded. Preview unavailable."
        )
    }

    func testDecodeFailureBeforeUploadDoesNotClaimUploadSucceeded() throws {
        let preview = LocalImagePreview(data: Data("not an image".utf8), uploadComplete: false)

        XCTAssertEqual(
            try preview.inspect().find(text: "Preview unavailable.").string(),
            "Preview unavailable."
        )
    }

    func testAbsentThumbnailAfterSuccessfulUploadKeepsAccessibleFallback() throws {
        let preview = LocalImagePreview(data: nil)

        XCTAssertEqual(
            try preview.inspect().find(text: "Image uploaded. Preview unavailable.").string(),
            "Image uploaded. Preview unavailable."
        )
    }

    func testInvalidSelectedBytesDoNotBecomeRetainedThumbnail() {
        XCTAssertNil(LocalImagePreview.thumbnailData(from: Data(repeating: 0x41, count: 512 * 1_024)))
    }

    func testSupportedImageBytesRenderAsAnImage() throws {
        let data = try XCTUnwrap(Data(base64Encoded: "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAIBRAA7"))
        let preview = LocalImagePreview(data: data)

        XCTAssertNoThrow(try preview.inspect().find(ViewType.Image.self))
    }
}
