import Foundation
import SwiftUI
import ViewInspector
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeTopicHeldImageUploadTests: NativeRouteSurfaceViewModelTestCase {
    func testTopicImageFieldUsesOnlyReturnedPlacementForPersistedPreview() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            TopicEnvelope.self,
            from: ApiFixtureLoader.data("web.topics.mutation.default")
        )
        let topic = response.topic
        XCTAssertEqual(topic.logoImagePlacement?.placementId, "00000000-0000-7000-8000-000000000306")
        XCTAssertEqual(topic.logoImagePlacement?.placementRevision, 3)
        XCTAssertEqual(topic.heroImagePlacement?.placementId, "00000000-0000-7000-8000-000000000307")
        XCTAssertEqual(topic.heroImagePlacement?.placementRevision, 4)
    }

    func testManualImageIdEditInvalidatesHeldTopicUploadAndPreview() async throws {
        var imageId = "original-image"
        let placementDecoder = JSONDecoder()
        placementDecoder.keyDecodingStrategy = .convertFromSnakeCase
        var placement: ImagePlacement? = try placementDecoder.decode(
            ImagePlacement.self,
            from: Data(#"{"image_id":"original-image","placement_id":"original-placement","placement_revision":1}"#
                .utf8)
        )
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("topic-pending-\(UUID().uuidString).png")
        try Data("selected-bytes".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        CannedFeedURLProtocol.queuedHandlers["/api/v1/images/upload-url"] = [(
            Data(
                #"{"upload":{"image_id":"server-image","upload_url":"https://upload.example.test/server-image","content_type":"image/png","expires_at":null}}"#
                    .utf8
            ),
            201,
            0
        )]
        CannedFeedURLProtocol.handlers["/server-image"] = (Data(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/server-image/completions"] = (
            Data(#"{"image":{"id":"server-image","upload_status":"processing"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/images/server-image/upload-state"] = (
            Data(#"{"upload_state":{"id":"server-image","upload_status":"complete","ready":true,"blocked":false}}"#
                .utf8),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/images/upload-url")
        defer { CannedFeedURLProtocol.releaseResponse(path: "/api/v1/images/upload-url") }
        let imageIdBinding = Binding(get: { imageId }, set: { imageId = $0 })
        let placementBinding = Binding(get: { placement }, set: { placement = $0 })
        let uploadState = NativeTopicImageFieldUploadState()
        let sut = try NativeTopicImageField(
            title: .nativeSwiftTopicManagementFieldsLogoImage,
            previewWidth: 64,
            imageId: imageIdBinding,
            placement: placementBinding,
            client: makeClient(),
            uploadSession: URLSession(configuration: .ephemeral),
            uploadState: uploadState
        )

        try await ViewHosting.host(sut) {
            let request = CannedFeedURLProtocol.requestBarrier(path: "/api/v1/images/upload-url", method: "POST")
            let upload = Task { await sut.uploadImage(at: url) }
            _ = try await request.wait()
            XCTAssertTrue(uploadState.isUploading)
            XCTAssertEqual(uploadState.localPreviewData, Data("selected-bytes".utf8))
            XCTAssertEqual(
                try sut.inspect().find(LocalImagePreview.self).actualView().data,
                Data("selected-bytes".utf8)
            )

            imageId = "manual-image"
            sut.handleImageIdChange(imageId)
            XCTAssertNil(uploadState.localPreviewData)
            XCTAssertFalse(uploadState.isUploading)
            XCTAssertNil(placement)

            CannedFeedURLProtocol.releaseResponse(path: "/api/v1/images/upload-url")
            await upload.value
            XCTAssertEqual(imageId, "manual-image")
            XCTAssertNil(uploadState.localPreviewData)
        }
    }
}
