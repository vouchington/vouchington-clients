import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativePostComposeImageLimitTests: XCTestCase {
    private let limitsPath = "/api/v1/content-limits"

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testFetchedCapBlocksUploadPastTheLimitAndShowsTheMaxMessage() async throws {
        CannedFeedURLProtocol.handlers[limitsPath] = (limits(imagesMax: 1), 200)
        let vm = makeViewModel()
        await vm.loadImageLimit()
        XCTAssertEqual(vm.imageLimit, 1)
        registerUploadFlow(imageId: "image-1")

        try await vm.uploadImages(from: makeImageFiles(count: 2))

        XCTAssertEqual(vm.images.count, 1)
        XCTAssertFalse(vm.canAddMoreImages)
        XCTAssertEqual(
            vm.imageUploadErrorMessage,
            .app(UiMessage(.sharedPostImageLimitMax, numberParameters: ["max": 1]))
        )
    }

    func testFailedFetchLeavesTheComposerUncapped() async {
        CannedFeedURLProtocol.handlers[limitsPath] = (Data("{}".utf8), 500)
        let vm = makeViewModel()
        vm.imageLimit = 1

        await vm.loadImageLimit()

        XCTAssertNil(vm.imageLimit)
        XCTAssertTrue(vm.canAddMoreImages)
    }

    func testLoadWithoutAClientDoesNothing() async {
        let vm = NativePostComposeViewModel(client: nil)
        await vm.loadImageLimit()
        XCTAssertNil(vm.imageLimit)
        XCTAssertTrue(vm.canAddMoreImages)
    }

    func testLimitResponseFromBeforeLeavingTheSurfaceIsIgnored() async {
        CannedFeedURLProtocol.handlers[limitsPath] = (limits(imagesMax: 1), 200)
        CannedFeedURLProtocol.suspendResponse(path: limitsPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: limitsPath) }
        let vm = makeViewModel()
        let staleLoad = Task { await vm.loadImageLimit() }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: limitsPath) else {
            XCTFail("The limit request never reached the canned protocol")
            CannedFeedURLProtocol.releaseResponse(path: limitsPath)
            await staleLoad.value
            return
        }

        vm.clearImagePreviewsForNavigation()
        CannedFeedURLProtocol.releaseResponse(path: limitsPath)
        await staleLoad.value

        XCTAssertNil(vm.imageLimit)
    }

    func testPostLimitErrorCodesMapToTheirKeysForBothApiShapes() {
        let imageLimit = UiVerbatimText.app(UiMessage(.sharedPostImageLimit))
        let categoryLimit = UiVerbatimText.app(UiMessage(.sharedPostCategoryLimit))

        XCTAssertEqual(
            NativePostLimitErrorPresentation.message(for: VouchaError.api(
                statusCode: 422, preconditionCode: "POST_IMAGE_LIMIT_REACHED"
            )),
            imageLimit
        )
        XCTAssertEqual(
            NativePostLimitErrorPresentation.message(for: VouchaError.apiMessage(
                statusCode: 422, preconditionCode: "POST_CATEGORY_LIMIT_REACHED", message: "too many"
            )),
            categoryLimit
        )
        XCTAssertNil(NativePostLimitErrorPresentation.message(for: VouchaError.api(
            statusCode: 422, preconditionCode: "OTHER"
        )))
        XCTAssertNil(NativePostLimitErrorPresentation.message(for: VouchaError.notFound))
    }

    private func makeViewModel() -> NativePostComposeViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return NativePostComposeViewModel(
            client: client,
            imageUploadProtocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func limits(imagesMax: Int) -> Data {
        Data("""
        {"post_images_max":\(imagesMax),"copyright_claimant_targets_max":20,\
        "follower_distribution_recipients_max":100}
        """.utf8)
    }

    private func registerUploadFlow(imageId: String) {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/images/upload-url", default: []].append((
            Data("""
            {"upload":{"image_id":"\(imageId)","upload_url":"https://upload.example.test/\(imageId)",\
            "content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201,
            0
        ))
        CannedFeedURLProtocol.handlers["/\(imageId)"] = (Data(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/\(imageId)/completions"] = (
            Data(#"{"image":{"id":"\#(imageId)","upload_status":"processing"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/images/\(imageId)/upload-state"] = (
            Data(
                #"{"upload_state":{"id":"\#(imageId)","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
                    .utf8
            ),
            200
        )
    }

    private func makeImageFiles(count: Int) throws -> [URL] {
        try (1 ... count).map { index in
            let url = FileManager.default.temporaryDirectory
                .appendingPathComponent("limit-\(index)-\(UUID().uuidString)")
                .appendingPathExtension("jpg")
            try Data("image-\(index)".utf8).write(to: url)
            return url
        }
    }
}

extension UiVerbatimText {
    /// The `{max}` message the composer shows when the fetched image cap blocks an upload.
    static func postImageLimitReached(_ max: Int) -> UiVerbatimText {
        .app(UiMessage(.sharedPostImageLimitMax, numberParameters: ["max": Double(max)]))
    }
}
