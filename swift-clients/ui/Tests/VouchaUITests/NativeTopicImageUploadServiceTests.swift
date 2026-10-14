import Foundation
@testable import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeTopicImageUploadServiceTests: XCTestCase {
    override func setUp() {
        super.setUp()
        TopicImageUploadURLProtocol.reset()
    }

    override func tearDown() {
        TopicImageUploadURLProtocol.reset()
        super.tearDown()
    }

    private func makeUploadClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [TopicImageUploadURLProtocol.self]
        )
    }

    func testUploadsAndCompletesImage() async throws {
        configureSuccessfulUpload()
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [TopicImageUploadURLProtocol.self]
        let session = URLSession(configuration: configuration)
        let imageURL = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("topic-image.png")
        try Data([0x89, 0x50, 0x4E, 0x47]).write(to: imageURL)
        defer { try? FileManager.default.removeItem(at: imageURL) }
        let service = try NativeTopicImageUploadService(
            client: makeUploadClient(),
            session: session,
            uploadStatePollDelayNanoseconds: 0
        )

        let imageId = try await service.uploadImage(data: Data(contentsOf: imageURL), contentType: "image/png")

        XCTAssertEqual(imageId, "image-ready")
        XCTAssertEqual(TopicImageUploadURLProtocol.capturedMethods, ["POST", "PUT", "POST", "GET"])
        XCTAssertEqual(TopicImageUploadURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/images/upload-url",
            "/upload",
            "/api/v1/images/image-1/completions",
            "/api/v1/images/image-1/upload-state"
        ])
        let presignBody = try XCTUnwrap(TopicImageUploadURLProtocol.capturedBodies.first ?? nil)
        XCTAssertTrue(presignBody.contains(#""content_length":4"#))
        XCTAssertTrue(presignBody.contains("content_type"))
    }

    private func configureSuccessfulUpload() {
        TopicImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {
              "upload": {
                "image_id": "image-1",
                "upload_url": "http://localhost:2999/upload",
                "content_type": "image/png",
                "expires_at": null
              }
            }
            """.utf8),
            200
        )
        TopicImageUploadURLProtocol.handlers["/upload"] = (Data("{}".utf8), 200)
        TopicImageUploadURLProtocol.handlers["/api/v1/images/image-1/completions"] = (
            Data(#"{"image":{"id":"image-1","upload_status":"processing"}}"#.utf8),
            200
        )
        TopicImageUploadURLProtocol.handlers["/api/v1/images/image-1/upload-state"] = (
            Data(#"{"upload_state":{"id":"image-ready","upload_status":"complete","ready":true,"blocked":false}}"#
                .utf8),
            200
        )
    }

    func testRejectsMissingClientAndBadUploadResponses() async throws {
        let serviceWithoutClient = NativeTopicImageUploadService(client: nil)
        let imageURL = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("topic-image-error.png")
        try Data([0x89, 0x50, 0x4E, 0x47]).write(to: imageURL)
        defer { try? FileManager.default.removeItem(at: imageURL) }

        do {
            _ = try await serviceWithoutClient.uploadImage(
                data: Data(contentsOf: imageURL),
                contentType: "image/png"
            )
            XCTFail("Expected missing client upload to throw")
        } catch {
            XCTAssertFalse(error.localizedDescription.isEmpty)
        }

        TopicImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {
              "upload": {
                "image_id": "image-1",
                "upload_url": "http://localhost:2999/upload",
                "content_type": "image/png",
                "expires_at": null
              }
            }
            """.utf8),
            200
        )
        TopicImageUploadURLProtocol.handlers["/upload"] = (Data("{}".utf8), 500)
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [TopicImageUploadURLProtocol.self]
        let failingService = try NativeTopicImageUploadService(
            client: makeUploadClient(),
            session: URLSession(configuration: configuration)
        )

        do {
            _ = try await failingService.uploadImage(
                data: Data(contentsOf: imageURL),
                contentType: "image/png"
            )
            XCTFail("Expected failed upload response to throw")
        } catch {
            XCTAssertFalse(error.localizedDescription.isEmpty)
        }

        XCTAssertEqual(TopicImageUploadURLProtocol.capturedMethods, ["POST", "PUT"])

        TopicImageUploadURLProtocol.reset()
        TopicImageUploadURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"image-1","upload_url":"http://localhost:2999/upload",
            "content_type":"image/png","expires_at":null}}
            """.utf8),
            200
        )
        TopicImageUploadURLProtocol.handlers["/upload"] = (Data("{}".utf8), 200)
        TopicImageUploadURLProtocol.handlers["/api/v1/images/image-1/completions"] = (
            Data(#"{"image":{"id":"image-1","upload_status":"processing"}}"#.utf8),
            200
        )
        TopicImageUploadURLProtocol.handlers["/api/v1/images/image-1/upload-state"] = (
            Data(#"{"upload_state":{"id":"image-1","upload_status":"complete","ready":false,"blocked":true}}"#.utf8),
            200
        )
        let blockedService = try NativeTopicImageUploadService(
            client: makeUploadClient(),
            session: URLSession(configuration: configuration),
            maxUploadStatePolls: 1,
            uploadStatePollDelayNanoseconds: 0
        )

        do {
            _ = try await blockedService.uploadImage(
                data: Data(contentsOf: imageURL),
                contentType: "image/png"
            )
            XCTFail("Expected blocked upload state to throw")
        } catch {
            XCTAssertFalse(error.localizedDescription.isEmpty)
        }

        XCTAssertEqual(TopicImageUploadURLProtocol.capturedMethods, ["POST", "PUT", "POST", "GET"])
    }

    func testRejectsOversizedFileBeforeRequestingAnUploadURL() async throws {
        let imageURL = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("topic-image-oversized.png")
        FileManager.default.createFile(atPath: imageURL.path, contents: nil)
        let handle = try FileHandle(forWritingTo: imageURL)
        try handle.truncate(atOffset: 50 * 1_024 * 1_024 + 1)
        try handle.close()
        defer { try? FileManager.default.removeItem(at: imageURL) }

        let service = try NativeTopicImageUploadService(client: makeUploadClient())

        do {
            _ = try await service.uploadImage(
                data: Data(repeating: 0, count: 50 * 1_024 * 1_024 + 1),
                contentType: "image/png"
            )
            XCTFail("Expected oversized image to be rejected")
        } catch let VouchaError.api(_, preconditionCode) {
            XCTAssertEqual(preconditionCode, "IMAGE_TOO_LARGE")
        }
        XCTAssertTrue(TopicImageUploadURLProtocol.capturedMethods.isEmpty)
    }
}

/// Scoped to image-upload tests so unrelated route tests cannot change their request trace.
private final class TopicImageUploadURLProtocol: URLProtocol {
    private struct CapturedRequest {
        let url: URL
        let method: String
        let body: String?
    }

    private static let lock = NSLock()
    private static var responseHandlers: [String: (Data, Int)] = [:]
    private static var requests: [CapturedRequest] = []

    static var handlers: [String: (Data, Int)] {
        get { withLock { responseHandlers } }
        set { withLock { responseHandlers = newValue } }
        _modify {
            lock.lock()
            defer { lock.unlock() }
            yield &responseHandlers
        }
    }

    static var capturedMethods: [String] {
        withLock { requests.map(\.method) }
    }

    static var capturedURLs: [URL] {
        withLock { requests.map(\.url) }
    }

    static var capturedBodies: [String?] {
        withLock { requests.map(\.body) }
    }

    static func reset() {
        withLock {
            responseHandlers = [:]
            requests = []
        }
    }

    private static func withLock<Value>(_ operation: () -> Value) -> Value {
        lock.lock()
        defer { lock.unlock() }
        return operation()
    }

    private let deliveryLock = NSLock()
    private var stopped = false

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.badURL))
            return
        }
        let body = Self.capturedBody(from: request)
        let (data, status) = Self.withLock {
            Self.requests.append(CapturedRequest(
                url: url,
                method: request.httpMethod ?? "GET",
                body: body
            ))
            return Self.responseHandlers[url.path] ?? (Data(), 404)
        }
        guard let response = HTTPURLResponse(
            url: url,
            statusCode: status,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        ) else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped else { return }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        stopped = true
        deliveryLock.unlock()
    }

    private static func capturedBody(from request: URLRequest) -> String? {
        if let body = request.httpBody { return String(data: body, encoding: .utf8) }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: 1_024)
        defer { buffer.deallocate() }
        while stream.hasBytesAvailable {
            let count = stream.read(buffer, maxLength: 1_024)
            guard count > 0 else { break }
            data.append(buffer, count: count)
        }
        return String(data: data, encoding: .utf8)
    }
}
