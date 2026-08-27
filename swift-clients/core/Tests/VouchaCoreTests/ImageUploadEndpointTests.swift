import Foundation
@testable import VouchaAPI
import XCTest

final class ImageUploadEndpointTests: XCTestCase {
    func testImageUploadEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.requestImageUploadURL(contentType: "image/jpeg", contentLength: 1_234),
            method: .POST,
            path: "/api/v1/images/upload-url",
            body: ["content_type": "image/jpeg", "content_length": 1_234]
        )
        assertEndpoint(
            Endpoint.completeImageUpload(imageId: "image 1"),
            method: .POST,
            path: "/api/v1/images/image%201/completions"
        )
        assertEndpoint(
            Endpoint.imageUploadState(imageId: "image 1"),
            method: .GET,
            path: "/api/v1/images/image%201/upload-state"
        )
    }

    func testUpdateIdentityEndpointEncodesProfileImageId() {
        assertEndpoint(
            Endpoint.updateIdentity(profileImageId: .value("image-1")),
            method: .PATCH,
            path: "/api/v1/my/identity",
            body: ["profile_image_id": "image-1"]
        )
        assertEndpoint(
            Endpoint.updateIdentity(profileImageId: .null),
            method: .PATCH,
            path: "/api/v1/my/identity",
            body: ["profile_image_id": NSNull()]
        )
    }
}
