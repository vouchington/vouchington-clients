import Foundation
import SwiftUI
import ViewInspector
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeTopicLogoLocalPreviewTests: NativeRouteSurfaceViewModelTestCase {
    func testSelectedLogoBytesRenderWhileUploadIsHeld() async throws {
        let topic = try fixtureTopic()
        let placement = try XCTUnwrap(topic.logoImagePlacement)
        assertFixtureLogo(placement)
        let imageBaseURL = try XCTUnwrap(URL(string: "https://images.test"))
        let expectedPlacementURL = try expectedLogoURL(placement, imageBaseURL: imageBaseURL)
        var imageId = try XCTUnwrap(topic.logoImageId)
        var boundPlacement: ImagePlacement? = placement
        let selected = try XCTUnwrap(Data(base64Encoded: Self.onePixelGif))
        let thumbnail = try XCTUnwrap(LocalImagePreview.thumbnailData(from: selected))
        let url = try writeTemporaryImage(selected, pathExtension: "gif")
        defer { try? FileManager.default.removeItem(at: url) }
        let path = "/api/v1/images/upload-url"
        registerReadyUpload()
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let state = NativeTopicImageFieldUploadState()
        let field = try makeField(
            imageId: Binding(get: { imageId }, set: { imageId = $0 }),
            placement: Binding(get: { boundPlacement }, set: { boundPlacement = $0 }),
            imageBaseURL: imageBaseURL,
            uploadState: state
        )

        try await ViewHosting.host(field) {
            XCTAssertEqual(try renderedPlacementURL(field), expectedPlacementURL)
            let request = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
            field.beginUpload(at: url)
            let task = try XCTUnwrap(state.uploadTask)
            do {
                _ = try await request.wait(timeout: .seconds(3))
                let preview = try field.inspect().find(LocalImagePreview.self).actualView()
                XCTAssertEqual(preview.data, thumbnail)
                XCTAssertFalse(preview.uploadComplete)
                XCTAssertNoThrow(try field.inspect().find(ViewType.Image.self))
                XCTAssertThrowsError(try field.inspect().find(text: "Preview unavailable."))
                XCTAssertTrue(try field.inspect().findAll(ViewType.View<AsyncImageView>.self).isEmpty)
                XCTAssertEqual(boundPlacement, placement)
                CannedFeedURLProtocol.releaseResponse(path: path)
                await task.value
            } catch {
                CannedFeedURLProtocol.releaseResponse(path: path)
                await task.value
                throw error
            }
            XCTAssertNil(state.uploadError)
            XCTAssertEqual(imageId, "image-ready")
            XCTAssertNil(boundPlacement)
            let completed = try field.inspect().find(LocalImagePreview.self).actualView()
            XCTAssertEqual(completed.data, thumbnail)
            XCTAssertTrue(completed.uploadComplete)
            XCTAssertThrowsError(try field.inspect().find(text: "Image uploaded. Preview unavailable."))
            XCTAssertTrue(try field.inspect().findAll(ViewType.View<AsyncImageView>.self).isEmpty)
        }
    }

    func testUndecodableLogoShowsAccessibleFallbackInsteadOfFixturePlacement() async throws {
        let topic = try fixtureTopic()
        let placement = try XCTUnwrap(topic.logoImagePlacement)
        assertFixtureLogo(placement)
        let imageBaseURL = try XCTUnwrap(URL(string: "https://images.test"))
        let expectedPlacementURL = try expectedLogoURL(placement, imageBaseURL: imageBaseURL)
        var imageId = try XCTUnwrap(topic.logoImageId)
        var boundPlacement: ImagePlacement? = placement
        let url = try writeTemporaryImage(Data("not-an-image".utf8), pathExtension: "gif")
        defer { try? FileManager.default.removeItem(at: url) }
        registerReadyUpload()
        let state = NativeTopicImageFieldUploadState()
        let field = try makeField(
            imageId: Binding(get: { imageId }, set: { imageId = $0 }),
            placement: Binding(get: { boundPlacement }, set: { boundPlacement = $0 }),
            imageBaseURL: imageBaseURL,
            uploadState: state
        )

        try await ViewHosting.host(field) {
            XCTAssertEqual(try renderedPlacementURL(field), expectedPlacementURL)
            field.beginUpload(at: url)
            let task = try XCTUnwrap(state.uploadTask)
            await task.value
            XCTAssertNil(state.uploadError)
            XCTAssertEqual(imageId, "image-ready")
            XCTAssertNotEqual(imageId, placement.imageId)
            XCTAssertNil(boundPlacement)
            let preview = try field.inspect().find(LocalImagePreview.self).actualView()
            XCTAssertNil(preview.data)
            XCTAssertTrue(preview.uploadComplete)
            XCTAssertEqual(
                try field.inspect().find(text: "Image uploaded. Preview unavailable.").string(),
                "Image uploaded. Preview unavailable."
            )
            XCTAssertTrue(try field.inspect().findAll(ViewType.View<AsyncImageView>.self).isEmpty)
        }
    }

    private func fixtureTopic() throws -> Topic {
        try JSONDecoder.vouchaFixtureDecoder.decode(
            TopicEnvelope.self,
            from: ApiFixtureLoader.data("web.topics.show.default")
        ).topic
    }

    private func assertFixtureLogo(_ placement: ImagePlacement) {
        XCTAssertEqual(placement.placementId, "00000000-0000-7000-8000-000000000306")
        XCTAssertEqual(placement.placementRevision, 3)
        XCTAssertEqual(placement.imageId, "00000000-0000-7000-8000-000000000205")
    }

    private func expectedLogoURL(_ placement: ImagePlacement, imageBaseURL: URL) throws -> String {
        let url = try XCTUnwrap(
            AppConfig(baseURL: AppConfig.shared.baseURL, imageBaseURL: imageBaseURL)
                .imageURL(for: placement, width: 96)
        )
        XCTAssertEqual(
            url,
            "https://images.test/images/placements/00000000-0000-7000-8000-000000000306/3/00000000-0000-7000-8000-000000000205?w=96"
        )
        return url
    }

    private func writeTemporaryImage(_ data: Data, pathExtension: String) throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("topic-logo-\(UUID().uuidString).\(pathExtension)")
        try data.write(to: url)
        return url
    }

    private func makeField(
        imageId: Binding<String>,
        placement: Binding<ImagePlacement?>,
        imageBaseURL: URL,
        uploadState: NativeTopicImageFieldUploadState
    ) throws -> NativeTopicImageField {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CannedFeedURLProtocol.self]
        return try NativeTopicImageField(
            title: .nativeSwiftTopicManagementFieldsLogoImage,
            previewWidth: 64,
            imageId: imageId,
            placement: placement,
            client: makeClient(),
            uploadSession: URLSession(configuration: configuration),
            imageBaseURL: imageBaseURL,
            uploadState: uploadState
        )
    }

    private func renderedPlacementURL(_ field: NativeTopicImageField) throws -> String {
        try XCTUnwrap(
            field.inspect().find(ViewType.View<AsyncImageView>.self).actualView().resolvedURLString
        )
    }

    private func registerReadyUpload() {
        CannedFeedURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data(
                """
                {"upload":{"image_id":"image-1","upload_url":"http://localhost:2999/upload","content_type":"image/gif","expires_at":null}}
                """.utf8
            ),
            201
        )
        CannedFeedURLProtocol.handlers["/upload"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/image-1/completions"] = (
            Data(#"{"image":{"id":"image-1","upload_status":"processing"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/images/image-1/upload-state"] = (
            Data(
                #"{"upload_state":{"id":"image-ready","upload_status":"complete","ready":true,"blocked":false}}"#.utf8
            ),
            200
        )
    }

    private static let onePixelGif = "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"
}
