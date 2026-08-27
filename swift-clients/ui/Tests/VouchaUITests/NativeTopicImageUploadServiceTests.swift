import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeTopicImageUploadServiceTests: NativeRouteSurfaceViewModelTestCase {
    func testUploadsAndCompletesImage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/images/upload-url"] = (
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
        CannedFeedURLProtocol.handlers["/upload"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/image-1/completions"] = (
            Data(#"{"image":{"id":"image-1","upload_status":"processing"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/images/image-1/upload-state"] = (
            Data(#"{"upload_state":{"id":"image-ready","upload_status":"complete","ready":true,"blocked":false}}"#
                .utf8),
            200
        )
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CannedFeedURLProtocol.self]
        let session = URLSession(configuration: configuration)
        let imageURL = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("topic-image.png")
        try Data([0x89, 0x50, 0x4E, 0x47]).write(to: imageURL)
        defer { try? FileManager.default.removeItem(at: imageURL) }
        let service = try NativeTopicImageUploadService(
            client: makeClient(),
            session: session,
            uploadStatePollDelayNanoseconds: 0
        )

        let imageId = try await service.uploadImage(at: imageURL)

        XCTAssertEqual(imageId, "image-ready")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "PUT", "POST", "GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/images/upload-url",
            "/upload",
            "/api/v1/images/image-1/completions",
            "/api/v1/images/image-1/upload-state"
        ])
        let presignBody = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.first ?? nil)
        XCTAssertTrue(presignBody.contains(#""content_length":4"#))
        XCTAssertTrue(presignBody.contains("content_type"))
    }

    func testRejectsMissingClientAndBadUploadResponses() async throws {
        let serviceWithoutClient = NativeTopicImageUploadService(client: nil)
        let imageURL = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("topic-image-error.png")
        try Data([0x89, 0x50, 0x4E, 0x47]).write(to: imageURL)
        defer { try? FileManager.default.removeItem(at: imageURL) }

        do {
            _ = try await serviceWithoutClient.uploadImage(at: imageURL)
            XCTFail("Expected missing client upload to throw")
        } catch {
            XCTAssertFalse(error.localizedDescription.isEmpty)
        }

        CannedFeedURLProtocol.handlers["/api/v1/images/upload-url"] = (
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
        CannedFeedURLProtocol.handlers["/upload"] = (Data("{}".utf8), 500)
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CannedFeedURLProtocol.self]
        let failingService = try NativeTopicImageUploadService(
            client: makeClient(),
            session: URLSession(configuration: configuration)
        )

        do {
            _ = try await failingService.uploadImage(at: imageURL)
            XCTFail("Expected failed upload response to throw")
        } catch {
            XCTAssertFalse(error.localizedDescription.isEmpty)
        }

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.suffix(2), ["POST", "PUT"])

        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.handlers["/upload"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/image-1/completions"] = (
            Data(#"{"image":{"id":"image-1","upload_status":"processing"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/images/image-1/upload-state"] = (
            Data(#"{"upload_state":{"id":"image-1","upload_status":"complete","ready":false,"blocked":true}}"#.utf8),
            200
        )
        let blockedService = try NativeTopicImageUploadService(
            client: makeClient(),
            session: URLSession(configuration: configuration),
            maxUploadStatePolls: 1,
            uploadStatePollDelayNanoseconds: 0
        )

        do {
            _ = try await blockedService.uploadImage(at: imageURL)
            XCTFail("Expected blocked upload state to throw")
        } catch {
            XCTAssertFalse(error.localizedDescription.isEmpty)
        }

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "PUT", "POST", "GET"])
    }
}
