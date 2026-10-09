import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

#if !SKIP && canImport(ImageIO) && canImport(CoreGraphics)
    import CoreGraphics
    import ImageIO
    import UniformTypeIdentifiers
#endif

@MainActor
final class NativePostComposePendingImagePreviewTests: NativeRouteSurfaceViewModelTestCase {
    func testNavigationCancelsRetainedBatchAndReleasesBusyOnlyAfterTaskExits() async throws {
        let vm = try NativePostComposeViewModel(
            client: makeClient(), imageUploadProtocolClasses: [CannedFeedURLProtocol.self]
        )
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("abandoned-compose-\(UUID().uuidString).png")
        try Data("selected-bytes".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        let path = "/api/v1/images/upload-url"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"{"upload":{"image_id":"abandoned","upload_url":"https://upload.example.test/abandoned","content_type":"image/png","expires_at":null}}"#
                    .utf8
            ), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let request = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        vm.startImageUploadBatch(from: [url])
        let upload = try XCTUnwrap(vm.imageUploadTask)
        do {
            _ = try await request.wait()
            XCTAssertTrue(vm.isUploadingImages)
            vm.clearImagePreviewsForNavigation()
            XCTAssertTrue(upload.isCancelled)
            XCTAssertTrue(vm.images.isEmpty)
            if vm.imageUploadTask != nil {
                XCTAssertTrue(vm.isUploadingImages)
                vm.startImageUploadBatch(from: [url])
                XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
            }
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await upload.value
            throw error
        }
        CannedFeedURLProtocol.releaseResponse(path: path)
        await upload.value
        XCTAssertNil(vm.imageUploadTask)
        XCTAssertFalse(vm.isUploadingImages)
        XCTAssertTrue(vm.pendingImagePreviews.isEmpty)
        XCTAssertTrue(vm.images.isEmpty)
    }

    func testNavigationBeforeScheduledBatchStartsDoesNotReadSelectedFile() async throws {
        let vm = try NativePostComposeViewModel(
            client: makeClient(), imageUploadProtocolClasses: [CannedFeedURLProtocol.self]
        )
        let missingURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("not-read-\(UUID().uuidString).png")
        vm.startImageUploadBatch(from: [missingURL])
        let upload = try XCTUnwrap(vm.imageUploadTask)
        XCTAssertTrue(vm.isUploadingImages)
        vm.clearImagePreviewsForNavigation()
        await upload.value
        XCTAssertNil(vm.imageUploadTask)
        XCTAssertFalse(vm.isUploadingImages)
        XCTAssertNil(vm.imageUploadErrorMessage)
        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.isEmpty)
    }

    func testSelectedBytesPreviewWhileFirstUploadRequestIsHeld() async throws {
        let vm = try NativePostComposeViewModel(
            client: makeClient(),
            imageUploadProtocolClasses: [CannedFeedURLProtocol.self]
        )
        #if !SKIP && canImport(ImageIO) && canImport(CoreGraphics)
            let selectedData = try makeWidePNG()
            let contentType = "image/png"
            let extensionName = "png"
        #else
            let selectedData =
                try XCTUnwrap(Data(base64Encoded: "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"))
            let contentType = "image/gif"
            let extensionName = "gif"
        #endif
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("pending-preview-\(UUID().uuidString).\(extensionName)")
        try selectedData.write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        CannedFeedURLProtocol.queuedHandlers["/api/v1/images/upload-url"] = [(
            Data(
                "{\"upload\":{\"image_id\":\"pending-image\",\"upload_url\":\"https://upload.example.test/pending-image\",\"content_type\":\"\(contentType)\",\"expires_at\":null}}"
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
        do {
            _ = try await uploadRequest.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: "/api/v1/images/upload-url")
            await upload.value
            throw error
        }

        XCTAssertTrue(vm.images.isEmpty)
        #if !SKIP && canImport(ImageIO) && canImport(CoreGraphics)
            let pendingThumbnail = try XCTUnwrap(vm.pendingImagePreviews.first?.data)
            XCTAssertNotEqual(pendingThumbnail, selectedData)
            XCTAssertLessThanOrEqual(pendingThumbnail.count, 256 * 1_024)
            let pendingSource = try XCTUnwrap(CGImageSourceCreateWithData(pendingThumbnail as CFData, nil))
            let pendingImage = try XCTUnwrap(CGImageSourceCreateImageAtIndex(pendingSource, 0, nil))
            XCTAssertEqual(pendingImage.width, 128)
            XCTAssertEqual(pendingImage.height, 32)
        #else
            XCTAssertEqual(vm.pendingImagePreviews.count, 1)
            XCTAssertNil(vm.pendingImagePreviews.first?.data)
        #endif

        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/images/upload-url")
        await upload.value
        XCTAssertTrue(vm.pendingImagePreviews.isEmpty)
        XCTAssertEqual(vm.images.map(\.imageId), ["pending-image"])
        #if !SKIP && canImport(ImageIO) && canImport(CoreGraphics)
            let thumbnail = try XCTUnwrap(vm.images.first?.localPreviewData)
            XCTAssertLessThanOrEqual(thumbnail.count, 256 * 1_024)
            XCTAssertNotEqual(thumbnail, selectedData)
            XCTAssertEqual(thumbnail, pendingThumbnail)
        #else
            XCTAssertNil(vm.images.first?.localPreviewData)
        #endif
    }

    #if !SKIP && canImport(ImageIO) && canImport(CoreGraphics)
        func testSuccessfulUploadThumbnailDownsamplesWideImageTo128Pixels() throws {
            let original = try makeWidePNG()
            let thumbnail = try XCTUnwrap(LocalImagePreview.thumbnailData(from: original))
            let source = try XCTUnwrap(CGImageSourceCreateWithData(thumbnail as CFData, nil))
            let image = try XCTUnwrap(CGImageSourceCreateImageAtIndex(source, 0, nil))

            XCTAssertEqual(image.width, 128)
            XCTAssertEqual(image.height, 32)
            XCTAssertLessThanOrEqual(thumbnail.count, 256 * 1_024)
        }

        private func makeWidePNG() throws -> Data {
            let context = try XCTUnwrap(CGContext(
                data: nil,
                width: 2_048,
                height: 512,
                bitsPerComponent: 8,
                bytesPerRow: 0,
                space: CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            ))
            context.setFillColor(CGColor(red: 0.2, green: 0.4, blue: 0.6, alpha: 1))
            context.fill(CGRect(x: 0, y: 0, width: 2_048, height: 512))
            let image = try XCTUnwrap(context.makeImage())
            let encoded = try XCTUnwrap(CFDataCreateMutable(kCFAllocatorDefault, 0))
            let destination = try XCTUnwrap(CGImageDestinationCreateWithData(
                encoded,
                UTType.png.identifier as CFString,
                1,
                nil
            ))
            CGImageDestinationAddImage(destination, image, nil)
            XCTAssertTrue(CGImageDestinationFinalize(destination))
            let bytes = try XCTUnwrap(CFDataGetBytePtr(encoded))
            return Data(bytes: bytes, count: CFDataGetLength(encoded))
        }
    #endif

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
