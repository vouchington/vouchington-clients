import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativePostComposeImageTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testAddingImagesUploadsAndLimitsToTwenty() async throws {
        let vm = try makeViewModel()
        let urls = try makeImageFiles(count: 21, extension: "jpg")
        for index in 0 ..< 20 {
            try registerImageUploadFlow(
                imageId: "image-\(index + 1)",
                contentType: "image/jpeg",
                uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/image-\(index + 1)")),
                stateBody: #"{"upload_state":{"id":"image-\#(index + 1)","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
            )
        }

        await vm.uploadImages(from: urls)

        XCTAssertEqual(vm.images.count, 20)
        XCTAssertEqual(vm.imageUploadErrorMessage, .app(UiMessage(.nativeSwiftPostComposeImageMaximumReached)))
        XCTAssertEqual(vm.canAddMoreImages, false)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, 40)
    }

    func testUploadImagesWithoutClientAndFileReadFailureSurfaceErrors() async throws {
        let signedOutViewModel = NativePostComposeViewModel(client: nil)
        await signedOutViewModel.uploadImages(from: [])
        XCTAssertEqual(
            signedOutViewModel.imageUploadErrorMessage,
            .app(UiMessage(.nativeSwiftImageSelectionRequiresSignedInSession))
        )

        let viewModel = try makeViewModel()
        let missingURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("missing-\(UUID().uuidString)")
            .appendingPathExtension("jpg")

        await viewModel.uploadImages(from: [missingURL])

        XCTAssertTrue(viewModel.images.isEmpty)
        XCTAssertNotNil(viewModel.imageUploadErrorMessage)
    }

    func testUploadImagesIsBlockedWhilePublishing() async throws {
        let vm = try makeViewModel()
        vm.state = .loading
        let imageURL = try makeImageFile(name: "during-publish", extension: "jpg", data: Data("image".utf8))

        await vm.uploadImages(from: [imageURL])

        XCTAssertTrue(vm.images.isEmpty)
        XCTAssertEqual(vm.imageUploadErrorMessage, .app(UiMessage(.nativeSwiftPostComposeImageWaitForPublishing)))
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
    }

    func testImageSelectionLoaderRejectsMissingOrUnknownExtensions() async throws {
        let pngURL = try makeImageFile(name: "known", extension: "png", data: Data("png".utf8))
        let bmpURL = try makeImageFile(name: "bitmap", extension: "bmp", data: Data("bitmap".utf8))
        let unknownURL = try makeImageFile(name: "unknown", extension: "bin", data: Data("unknown".utf8))
        let missingExtensionURL = try makeImageFile(name: "missing", data: Data("missing".utf8))

        let (pngData, pngContentType) = try await ImageSelectionLoader.load(from: pngURL)
        XCTAssertEqual(pngData, Data("png".utf8))
        XCTAssertEqual(pngContentType, "image/png")

        do {
            _ = try await ImageSelectionLoader.load(from: unknownURL)
            XCTFail("Expected unknown image extension to be rejected.")
        } catch {
            XCTAssertEqual(
                try XCTUnwrap(error as? ImageSelectionError).message,
                UiMessage(.nativeSwiftImageSelectionUnsupportedImageExtension, parameters: ["extension": "bin"])
            )
        }

        do {
            _ = try await ImageSelectionLoader.load(from: bmpURL)
            XCTFail("Expected backend-unsupported image extension to be rejected.")
        } catch {
            XCTAssertEqual(
                try XCTUnwrap(error as? ImageSelectionError).message,
                UiMessage(.nativeSwiftImageSelectionUnsupportedImageExtension, parameters: ["extension": "bmp"])
            )
        }

        do {
            _ = try await ImageSelectionLoader.load(from: missingExtensionURL)
            XCTFail("Expected missing image extension to be rejected.")
        } catch {
            XCTAssertEqual(
                try XCTUnwrap(error as? ImageSelectionError).message,
                UiMessage(.nativeSwiftImageSelectionUnsupportedImageMissingExtension)
            )
        }
    }

    func testImageSelectionLoaderRejectsOversizedImageBeforeReadingData() async throws {
        let url = try makeSparseImageFile(
            name: "oversized",
            extension: "jpg",
            size: UInt64(50 * 1_024 * 1_024 + 1)
        )

        do {
            _ = try await ImageSelectionLoader.load(from: url)
            XCTFail("Expected oversized image to be rejected.")
        } catch {
            XCTAssertEqual(
                try XCTUnwrap(error as? ImageSelectionError).message,
                UiMessage(.nativeSwiftImageSelectionImageTooLarge)
            )
        }
    }

    func testBlockedImageUploadIsReportedAndExcluded() async throws {
        let vm = try makeViewModel()
        let url = try makeImageFile(name: "blocked", extension: "jpg", data: Data("blocked".utf8))
        try registerImageUploadFlow(
            imageId: "blocked-image",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/blocked-image")),
            stateBody: #"{"upload_state":{"id":"blocked-image","upload_status":"complete","upload_error":null,"ready":false,"blocked":true}}"#
        )

        await vm.uploadImages(from: [url])

        XCTAssertTrue(vm.images.isEmpty)
        XCTAssertEqual(vm.imageUploadErrorMessage, .app(UiMessage(.nativeSwiftImageSelectionBlocked)))
    }

    func testBatchUploadKeepsFailureVisibleAfterLaterSuccess() async throws {
        let vm = try makeViewModel()
        let urls = try makeImageFiles(count: 2, extension: "jpg")

        try registerImageUploadFlow(
            imageId: "blocked-image",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/blocked-batch-image")),
            stateBody: #"{"upload_state":{"id":"blocked-image","upload_status":"complete","upload_error":null,"ready":false,"blocked":true}}"#
        )
        try registerImageUploadFlow(
            imageId: "ready-image",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/ready-batch-image")),
            stateBody: #"{"upload_state":{"id":"ready-image","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )

        await vm.uploadImages(from: urls)

        let image = try XCTUnwrap(vm.images.first)
        XCTAssertEqual(vm.images.count, 1)
        XCTAssertEqual(image.imageId, "ready-image")
        XCTAssertEqual(vm.imageUploadErrorMessage, .app(UiMessage(.nativeSwiftImageSelectionBlocked)))
    }

    func testDuplicateCompletedImageIdsAreSkipped() async throws {
        let vm = try makeViewModel()
        let urls = try makeImageFiles(count: 2, extension: "jpg")

        try registerImageUploadFlow(
            imageId: "pending-image-a",
            completedImageId: "existing-image",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/pending-image-a")),
            stateBody: #"{"upload_state":{"id":"existing-image","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )
        try registerImageUploadFlow(
            imageId: "pending-image-b",
            completedImageId: "existing-image",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/pending-image-b")),
            stateBody: #"{"upload_state":{"id":"existing-image","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )

        await vm.uploadImages(from: urls)

        let image = try XCTUnwrap(vm.images.first)
        XCTAssertEqual(vm.images.count, 1)
        XCTAssertEqual(image.imageId, "existing-image")
        XCTAssertNil(vm.imageUploadErrorMessage)
    }

    func testUploadImagesRechecksCapAfterAsyncUploadBeforeAppending() async throws {
        let vm = try makeViewModel()
        vm.images = (0 ..< 19).map { NativePostComposeImageDraft(imageId: "existing-\($0)") }
        let url = try makeImageFile(name: "racing-cap", extension: "png", data: Data("racing-cap".utf8))
        try registerImageUploadFlow(
            imageId: "racing-image",
            contentType: "image/png",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/racing-image.png")),
            stateBody: #"{"upload_state":{"id":"racing-image","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#,
            uploadStateDelay: 0.2
        )

        let uploadTask = Task {
            await vm.uploadImages(from: [url])
        }
        for _ in 0 ..< 100 {
            if vm.isUploadingImages {
                break
            }
            await Task.yield()
        }
        XCTAssertTrue(vm.isUploadingImages)
        vm.images.append(.init(imageId: "external-fill"))

        await uploadTask.value

        XCTAssertEqual(vm.images.count, 20)
        XCTAssertFalse(vm.images.contains(where: { $0.imageId == "racing-image" }))
        XCTAssertEqual(vm.imageUploadErrorMessage, .app(UiMessage(.nativeSwiftPostComposeImageMaximumReached)))
    }

    func testImageCaptionReorderAndRemovalUpdateDraftOrder() async throws {
        let vm = try makeViewModel()
        let urls = try makeImageFiles(count: 2, extension: "png")

        try registerImageUploadFlow(
            imageId: "image-a",
            contentType: "image/png",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/image-a")),
            stateBody: #"{"upload_state":{"id":"image-a","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )
        try registerImageUploadFlow(
            imageId: "image-b",
            contentType: "image/png",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/image-b")),
            stateBody: #"{"upload_state":{"id":"image-b","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )

        await vm.uploadImages(from: urls)
        let firstId = try XCTUnwrap(vm.images.first?.id)
        let secondId = try XCTUnwrap(vm.images.dropFirst().first?.id)

        vm.updateImageCaption(id: firstId, caption: "Hero")
        vm.moveImageDown(id: firstId)

        XCTAssertEqual(vm.images.first?.imageId, "image-b")
        XCTAssertEqual(vm.images.last?.imageId, "image-a")
        XCTAssertEqual(vm.images.last?.caption, "Hero")

        vm.removeImage(id: secondId)
        XCTAssertEqual(vm.images.count, 1)
        XCTAssertEqual(vm.images.first?.imageId, "image-a")
        XCTAssertEqual(vm.images.first?.caption, "Hero")
    }

    func testImageCaptionIsCappedBeforePublish() async throws {
        let vm = try makeViewModel()
        let url = try makeImageFile(name: "long-caption", extension: "jpg", data: Data("long-caption".utf8))
        try registerImageUploadFlow(
            imageId: "long-caption-image",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/long-caption-image")),
            stateBody: #"{"upload_state":{"id":"long-caption-image","upload_status":"complete","upload_error":null,"ready":false,"blocked":false}}"#
        )

        await vm.uploadImages(from: [url])
        let imageId = try XCTUnwrap(vm.images.first?.id)
        let longCaption = String(repeating: "b", count: 1_200)

        vm.updateImageCaption(id: imageId, caption: longCaption)

        XCTAssertEqual(vm.images.first?.caption.count, 1_000)
        XCTAssertEqual(vm.imageInputs.first?.caption?.count, 1_000)

        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (makePostEnvelope(id: "post-caption"), 201)
        vm.title = "Native title"
        vm.bodyText = "Native body"
        vm.turnstileToken = "turnstile-token"

        await vm.publish()

        try assertPublishedBody(
            CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 },
            imageId: "long-caption-image",
            caption: String(repeating: "b", count: 1_000)
        )
    }

    func testImageSummaryInputsAndResetReflectCurrentImages() {
        let vm = NativePostComposeViewModel(client: nil)
        XCTAssertNil(vm.imageSummary)
        vm.images = [
            NativePostComposeImageDraft(imageId: "image-1", caption: "  Hero  "),
            NativePostComposeImageDraft(imageId: "image-2", caption: "   ")
        ]

        XCTAssertEqual(vm.imageSummary, .count(2, item: "image"))
        XCTAssertEqual(vm.imageInputs.map(\.imageId), ["image-1", "image-2"])
        XCTAssertEqual(vm.imageInputs.map(\.orderIndex), [0, 1])
        XCTAssertEqual(vm.imageInputs.first?.caption, "Hero")
        XCTAssertNil(vm.imageInputs.last?.caption)

        vm.resetImageFields()

        XCTAssertTrue(vm.images.isEmpty)
        XCTAssertFalse(vm.isUploadingImages)
        XCTAssertNil(vm.imageUploadErrorMessage)
    }

    func testPublishIncludesImagesInGlobalAndCommunityCreateBodies() async throws {
        let globalViewModel = try makeViewModel()
        let communityViewModel = try makeViewModel(communityIdOrSlug: "voucha")
        let imageURL = try makeImageFile(name: "compose", extension: "jpg", data: Data("compose-image".utf8))

        try registerImageUploadFlow(
            imageId: "compose-image-1",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/compose-image-1")),
            stateBody: #"{"upload_state":{"id":"compose-image-1","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )
        await globalViewModel.uploadImages(from: [imageURL])
        try globalViewModel.updateImageCaption(id: XCTUnwrap(globalViewModel.images.first?.id), caption: "Hero")
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (makePostEnvelope(id: "post-1"), 201)
        globalViewModel.title = "Native title"
        globalViewModel.bodyText = "Native body"
        globalViewModel.turnstileToken = "turnstile-token"

        await globalViewModel.publish()
        try assertPublishedBody(
            CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 },
            imageId: "compose-image-1",
            caption: "Hero"
        )

        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []

        try registerImageUploadFlow(
            imageId: "community-image-1",
            contentType: "image/jpeg",
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/community-image-1")),
            stateBody: #"{"upload_state":{"id":"community-image-1","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )
        await communityViewModel.uploadImages(from: [imageURL])
        try communityViewModel.updateImageCaption(
            id: XCTUnwrap(communityViewModel.images.first?.id),
            caption: "Community"
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/voucha/posts"] = (makePostEnvelope(id: "post-2"), 201)
        communityViewModel.title = "Native title"
        communityViewModel.bodyText = "Native body"
        communityViewModel.turnstileToken = "turnstile-token"

        await communityViewModel.publish()
        try assertPublishedBody(
            CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 },
            imageId: "community-image-1",
            caption: "Community"
        )
    }

    private func makeViewModel(communityIdOrSlug: String? = nil) throws -> NativePostComposeViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return NativePostComposeViewModel(
            client: apiClient,
            communityIdOrSlug: communityIdOrSlug,
            imageUploadProtocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func registerImageUploadFlow(
        imageId: String,
        completedImageId: String? = nil,
        contentType: String,
        uploadURL: URL,
        stateBody: String,
        uploadStateDelay: TimeInterval = 0
    ) {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/images/upload-url", default: []].append((
            Data("""
            {"upload":{"image_id":"\(imageId)","upload_url":"\(uploadURL
                .absoluteString)","content_type":"\(contentType)","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201,
            0
        ))
        CannedFeedURLProtocol.handlers[uploadURL.path] = (Data(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/\(imageId)/completions"] = (
            Data(#"{"image":{"id":"\#(completedImageId ?? imageId)","upload_status":"processing"}}"#.utf8),
            200
        )
        let completedImageId = completedImageId ?? imageId
        if uploadStateDelay > 0 {
            CannedFeedURLProtocol.queuedHandlers["/api/v1/images/\(completedImageId)/upload-state"] = [
                (Data(stateBody.utf8), 200, uploadStateDelay)
            ]
        } else {
            CannedFeedURLProtocol.handlers["/api/v1/images/\(completedImageId)/upload-state"] = (
                Data(stateBody.utf8),
                200
            )
        }
    }

    private func makeImageFiles(count: Int, extension fileExtension: String) throws -> [URL] {
        try (1 ... count).map { index in
            let url = FileManager.default.temporaryDirectory
                .appendingPathComponent("compose-\(index)-\(UUID().uuidString)")
                .appendingPathExtension(fileExtension)
            try Data("image-\(index)".utf8).write(to: url)
            return url
        }
    }

    private func makeImageFile(name: String, extension fileExtension: String, data: Data) throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("\(name)-\(UUID().uuidString)")
            .appendingPathExtension(fileExtension)
        try data.write(to: url)
        return url
    }

    private func makeImageFile(name: String, data: Data) throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("\(name)-\(UUID().uuidString)")
        try data.write(to: url)
        return url
    }

    private func makeSparseImageFile(name: String, extension fileExtension: String, size: UInt64) throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("\(name)-\(UUID().uuidString)")
            .appendingPathExtension(fileExtension)
        FileManager.default.createFile(atPath: url.path, contents: Data())
        let handle = try FileHandle(forWritingTo: url)
        try handle.truncate(atOffset: size)
        try handle.close()
        return url
    }

    private func makePostEnvelope(id: String) -> Data {
        Data("""
        {
          "post": {
            "id": "\(id)",
            "slug": null,
            "post_type": "discussion",
            "title": "Native title",
            "markdown": "Native body",
            "html": null,
            "parent_id": null,
            "root_id": null,
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": "pending"
          }
        }
        """.utf8)
    }

    private func assertPublishedBody(
        _ body: String?,
        imageId: String,
        caption: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        let body = try XCTUnwrap(body, file: file, line: line)
        let data = try XCTUnwrap(body.data(using: .utf8), file: file, line: line)
        let object = try JSONSerialization.jsonObject(with: data)
        let dictionary = try XCTUnwrap(object as? [String: Any], file: file, line: line)
        let images = try XCTUnwrap(dictionary["images"] as? [[String: Any]], file: file, line: line)
        XCTAssertEqual(images.count, 1, file: file, line: line)
        XCTAssertEqual(images.first?["image_id"] as? String, imageId, file: file, line: line)
        XCTAssertEqual(images.first?["order_index"] as? Int, 0, file: file, line: line)
        XCTAssertEqual(images.first?["caption"] as? String, caption, file: file, line: line)
    }
}
