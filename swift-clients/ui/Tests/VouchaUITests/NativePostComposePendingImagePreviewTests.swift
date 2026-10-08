import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativePostComposePendingImagePreviewTests: NativeRouteSurfaceViewModelTestCase {
    func testSelectedBytesPreviewWhileFirstUploadRequestIsHeld() async throws {
        let vm = try NativePostComposeViewModel(
            client: makeClient(),
            imageUploadProtocolClasses: [CannedFeedURLProtocol.self]
        )
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("pending-preview-\(UUID().uuidString).png")
        try Data("image-bytes".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        CannedFeedURLProtocol.queuedHandlers["/api/v1/images/upload-url"] = [(
            Data(
                #"{"upload":{"image_id":"pending-image","upload_url":"https://upload.example.test/pending-image","content_type":"image/png","expires_at":null}}"#
                    .utf8
            ),
            201,
            0
        )]
        CannedFeedURLProtocol.handlers["/pending-image"] = (Data(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/pending-image/completions"] = (
            Data(#"{"image":{"id":"pending-image","upload_status":"processing"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/images/pending-image/upload-state"] = (
            Data(#"{"upload_state":{"id":"pending-image","upload_status":"complete","ready":true,"blocked":false}}"#
                .utf8),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/images/upload-url")
        defer { CannedFeedURLProtocol.releaseResponse(path: "/api/v1/images/upload-url") }

        let uploadRequest = CannedFeedURLProtocol.requestBarrier(path: "/api/v1/images/upload-url", method: "POST")
        let upload = Task { await vm.uploadImages(from: [url]) }
        _ = try await uploadRequest.wait()

        XCTAssertTrue(vm.images.isEmpty)
        XCTAssertEqual(vm.pendingImagePreviews.map(\.data), [Data("image-bytes".utf8)])

        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/images/upload-url")
        await upload.value
        XCTAssertTrue(vm.pendingImagePreviews.isEmpty)
    }

    func testImageSelectionLoaderKeepsJpegExtensionSupportedWithoutRegisteredUti() async throws {
        let jpegURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("known-jpeg-\(UUID().uuidString).jpg")
        try Data("jpeg".utf8).write(to: jpegURL)
        defer { try? FileManager.default.removeItem(at: jpegURL) }
        let (data, contentType) = try await ImageSelectionLoader.load(from: jpegURL)

        XCTAssertEqual(data, Data("jpeg".utf8))
        XCTAssertEqual(contentType, "image/jpeg")
    }
}
