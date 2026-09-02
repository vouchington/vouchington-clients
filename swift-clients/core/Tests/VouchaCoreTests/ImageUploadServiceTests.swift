import Foundation
@testable import VouchaAPI
@testable import VouchaCore
import VouchaModels
import XCTest

final class ImageUploadServiceTests: XCTestCase {
    override func setUp() {
        super.setUp()
        ImageUploadURLProtocol.handlers = [:]
        ImageUploadURLProtocol.queuedHandlers = [:]
        ImageUploadURLProtocol.capturedRequests = []
    }

    func testUploadImagePostsUploadUrlUploadsBinaryCallsCompletionAndPollsUntilReady() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-1"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-1","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-1"] = (Data(), 200)
        ImageUploadURLProtocol.handlers["/api/v1/images/image-1/completions"] = (
            Data(#"{"image":{"id":"image-1","upload_status":"processing"}}"#.utf8),
            200
        )
        ImageUploadURLProtocol.queuedHandlers["/api/v1/images/image-1/upload-state"] = [
            (
                Data(
                    #"{"upload_state":{"id":"image-1","upload_status":"processing","upload_error":null,"ready":false,"blocked":false}}"#
                        .utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"{"upload_state":{"id":"image-1","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
                        .utf8
                ),
                200,
                0
            )
        ]

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 1
        )
        let state = try await service.uploadImage(data: Data("binary-image-data".utf8), contentType: "image/jpeg")

        XCTAssertEqual(state.id, "image-1")
        XCTAssertTrue(state.ready)
        XCTAssertFalse(state.blocked)
        XCTAssertEqual(state.uploadStatus, .complete)
        XCTAssertEqual(
            ImageUploadURLProtocol.capturedRequests.map { $0.httpMethod ?? "" },
            ["POST", "PUT", "POST", "GET", "GET"]
        )
        XCTAssertEqual(
            ImageUploadURLProtocol.capturedRequests[1].value(forHTTPHeaderField: "Content-Type"),
            "image/jpeg"
        )
        XCTAssertEqual(ImageUploadURLProtocol.capturedRequests[1].value(forHTTPHeaderField: "Content-Length"), "17")
        XCTAssertNil(ImageUploadURLProtocol.capturedRequests[1].value(forHTTPHeaderField: "x-voucha-client"))
        XCTAssertNil(ImageUploadURLProtocol.capturedRequests[1].value(forHTTPHeaderField: "x-voucha-platform"))
        XCTAssertNil(ImageUploadURLProtocol.capturedRequests[1].value(forHTTPHeaderField: "x-voucha-app-version"))
        #if canImport(Darwin)
            XCTAssertEqual(ImageUploadURLProtocol.capturedBody(at: 1), "binary-image-data")
        #endif
    }

    func testUploadImageTreatsCompleteStateAsTerminalEvenWhenNotReadyOrBlocked() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-terminal"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-terminal","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-terminal"] = (Data(), 200)
        ImageUploadURLProtocol.handlers["/api/v1/images/image-terminal/completions"] = (
            Data(#"{"image":{"id":"image-terminal","upload_status":"processing"}}"#.utf8),
            200
        )
        ImageUploadURLProtocol.handlers["/api/v1/images/image-terminal/upload-state"] = (
            Data(
                #"{"upload_state":{"id":"image-terminal","upload_status":"complete","upload_error":null,"ready":false,"blocked":false}}"#
                    .utf8
            ),
            200
        )

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 1
        )
        let state = try await service.uploadImage(data: Data("terminal-image".utf8), contentType: "image/jpeg")

        XCTAssertEqual(state.id, "image-terminal")
        XCTAssertEqual(state.uploadStatus, .complete)
        XCTAssertFalse(state.ready)
        XCTAssertFalse(state.blocked)
        XCTAssertEqual(
            ImageUploadURLProtocol.capturedRequests.map { $0.httpMethod ?? "" },
            ["POST", "PUT", "POST", "GET"]
        )
    }

    func testUploadImageRetriesTransientCompletionFailures() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-completion-retry"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-completion-retry","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-completion-retry"] = (Data(), 200)
        ImageUploadURLProtocol.queuedHandlers["/api/v1/images/image-completion-retry/completions"] = [
            (Data(), 503, 0),
            (Data(#"{"image":{"id":"image-completion-retry","upload_status":"processing"}}"#.utf8), 200, 0)
        ]
        ImageUploadURLProtocol.handlers["/api/v1/images/image-completion-retry/upload-state"] = (
            Data(
                #"{"upload_state":{"id":"image-completion-retry","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
                    .utf8
            ),
            200
        )

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 1
        )
        let state = try await service.uploadImage(data: Data("retry-completion".utf8), contentType: "image/jpeg")

        XCTAssertEqual(state.id, "image-completion-retry")
        XCTAssertEqual(state.uploadStatus, .complete)
        XCTAssertEqual(
            ImageUploadURLProtocol.capturedRequests.map { $0.httpMethod ?? "" },
            ["POST", "PUT", "POST", "POST", "GET"]
        )
    }

    func testUploadImageRetriesTransientPollingFailures() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-polling-retry"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-polling-retry","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-polling-retry"] = (Data(), 200)
        ImageUploadURLProtocol.handlers["/api/v1/images/image-polling-retry/completions"] = (
            Data(#"{"image":{"id":"image-polling-retry","upload_status":"processing"}}"#.utf8),
            200
        )
        ImageUploadURLProtocol.queuedHandlers["/api/v1/images/image-polling-retry/upload-state"] = [
            (Data(), 429, 0),
            (
                Data(
                    #"{"upload_state":{"id":"image-polling-retry","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
                        .utf8
                ),
                200,
                0
            )
        ]

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 1
        )
        let state = try await service.uploadImage(data: Data("retry-polling".utf8), contentType: "image/jpeg")

        XCTAssertEqual(state.id, "image-polling-retry")
        XCTAssertTrue(state.ready)
        XCTAssertEqual(
            ImageUploadURLProtocol.capturedRequests.map { $0.httpMethod ?? "" },
            ["POST", "PUT", "POST", "GET", "GET"]
        )
    }

    func testUploadImageSleepsBetweenPollingAttempts() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-sleep"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-sleep","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-sleep"] = (Data(), 200)
        ImageUploadURLProtocol.handlers["/api/v1/images/image-sleep/completions"] = (
            Data(#"{"image":{"id":"image-sleep","upload_status":"processing"}}"#.utf8),
            200
        )
        ImageUploadURLProtocol.queuedHandlers["/api/v1/images/image-sleep/upload-state"] = [
            (
                Data(
                    #"{"upload_state":{"id":"image-sleep","upload_status":"processing","upload_error":null,"ready":false,"blocked":false}}"#
                        .utf8
                ),
                200,
                0.001
            ),
            (
                Data(
                    #"{"upload_state":{"id":"image-sleep","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
                        .utf8
                ),
                200,
                0
            )
        ]

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0.001,
            timeout: 1
        )

        let state = try await service.uploadImage(data: Data("sleep-image".utf8), contentType: "image/jpeg")

        XCTAssertTrue(state.ready)
        XCTAssertEqual(
            ImageUploadURLProtocol.capturedRequests.filter { $0.url?.path.contains("upload-state") == true }.count,
            2
        )
    }

    func testUploadImageStopsPollingWhenBlocked() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-2"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-2","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/png","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-2"] = (Data(), 200)
        ImageUploadURLProtocol.handlers["/api/v1/images/image-2/completions"] = (
            Data(#"{"image":{"id":"image-2","upload_status":"processing"}}"#.utf8),
            200
        )
        ImageUploadURLProtocol.handlers["/api/v1/images/image-2/upload-state"] = (
            Data(
                #"{"upload_state":{"id":"image-2","upload_status":"complete","upload_error":null,"ready":false,"blocked":true}}"#
                    .utf8
            ),
            200
        )

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 1
        )
        let state = try await service.uploadImage(data: Data("blocked-image".utf8), contentType: "image/png")

        XCTAssertEqual(state.id, "image-2")
        XCTAssertTrue(state.blocked)
        XCTAssertFalse(state.ready)
        XCTAssertEqual(
            ImageUploadURLProtocol.capturedRequests.map { $0.httpMethod ?? "" },
            ["POST", "PUT", "POST", "GET"]
        )
    }

    func testUploadImageThrowsOnTimeout() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-3"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-3","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/webp","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-3"] = (Data(), 200)
        ImageUploadURLProtocol.handlers["/api/v1/images/image-3/completions"] = (
            Data(#"{"image":{"id":"image-3","upload_status":"processing"}}"#.utf8),
            200
        )
        ImageUploadURLProtocol.handlers["/api/v1/images/image-3/upload-state"] = (
            Data(
                #"{"upload_state":{"id":"image-3","upload_status":"processing","upload_error":null,"ready":false,"blocked":false}}"#
                    .utf8
            ),
            200
        )

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 0
        )

        do {
            _ = try await service.uploadImage(data: Data("timeout-image".utf8), contentType: "image/webp")
            XCTFail("Expected timeout")
        } catch let error as ImageUploadServiceError {
            if case let .timeout(imageId) = error {
                XCTAssertEqual(imageId, "image-3")
            } else {
                XCTFail("Expected timeout error")
            }
        }
    }

    func testUploadImageThrowsOnBinaryUploadFailure() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/image-failed-put"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-failed-put","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        ImageUploadURLProtocol.handlers["/image-failed-put"] = (Data(), 503)

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 1
        )

        do {
            _ = try await service.uploadImage(data: Data("failed-put".utf8), contentType: "image/jpeg")
            XCTFail("Expected upload failure")
        } catch let error as ImageUploadServiceError {
            XCTAssertEqual(error.errorDescription, "Image upload failed with status code 503.")
        }
        XCTAssertEqual(ImageUploadServiceError.uploadFailed(statusCode: 0).errorDescription, "Image upload failed.")
        XCTAssertEqual(ImageUploadServiceError.timeout(imageId: "image-1").errorDescription, "Image upload timed out.")
    }

    func testUploadImageRejectsInsecureExternalUploadURLBeforeSendingBinary() async throws {
        let client = try makeClient()
        let uploadURL = try XCTUnwrap(URL(string: "http://upload.example.test/image-insecure"))

        ImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-insecure","upload_url":"\(uploadURL
                .absoluteString)","content_type":"image/jpeg","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )

        let service = ImageUploadService(
            client: client,
            uploadProtocolClasses: [ImageUploadURLProtocol.self],
            pollInterval: 0,
            timeout: 1
        )

        do {
            _ = try await service.uploadImage(data: Data("insecure-put".utf8), contentType: "image/jpeg")
            XCTFail("Expected insecure upload URL to be rejected")
        } catch let error as ImageUploadServiceError {
            XCTAssertEqual(error.errorDescription, "Invalid image upload URL.")
        }
        XCTAssertEqual(ImageUploadURLProtocol.capturedRequests.map { $0.httpMethod ?? "" }, ["POST"])
        XCTAssertEqual(ImageUploadServiceError.invalidUploadURL.errorDescription, "Invalid image upload URL.")
    }

    func testImageUploadTargetInitializerAndCapturedBodyBranches() throws {
        let uploadURL = try XCTUnwrap(URL(string: "https://upload.example.test/manual"))
        let expiresAt = Date(timeIntervalSince1970: 1)
        let target = ImageUploadTarget(
            imageId: "manual-image",
            uploadUrl: uploadURL,
            contentType: "image/jpeg",
            expiresAt: expiresAt
        )
        XCTAssertEqual(target.imageId, "manual-image")
        XCTAssertEqual(target.uploadUrl, uploadURL)
        XCTAssertEqual(target.contentType, "image/jpeg")
        XCTAssertEqual(target.expiresAt, expiresAt)

        var request = URLRequest(url: uploadURL)
        request.httpBody = Data("direct-body".utf8)
        ImageUploadURLProtocol.capturedRequests = [request]
        XCTAssertEqual(ImageUploadURLProtocol.capturedBody(at: 0), "direct-body")
    }

    private func makeClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [ImageUploadURLProtocol.self]
        )
    }
}
