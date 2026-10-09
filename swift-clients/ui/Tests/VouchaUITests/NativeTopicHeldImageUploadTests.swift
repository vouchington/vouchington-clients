import Foundation
import Observation
import SwiftUI
import ViewInspector
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeTopicHeldImageUploadTests: NativeRouteSurfaceViewModelTestCase {
    func testMountedTopicSaveWaitsForHeldLogoAndHeroImageWorkflows() async throws {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("topic-save-held-\(UUID().uuidString).png")
        try Data("selected-bytes".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        let path = "/api/v1/images/upload-url"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"{"upload":{"image_id":"new-logo","upload_url":"https://upload.example.test/new-logo","content_type":"image/png","expires_at":null}}"#
                    .utf8
            ), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let logoState = NativeTopicImageFieldUploadState()
        let heroState = NativeTopicImageFieldUploadState()
        let createMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create")?.match)
        let sut = try NativeTopicManagementSurface(
            client: makeClient(), routeMatch: createMatch,
            logoUploadState: logoState, heroUploadState: heroState
        )

        try await ViewHosting.host(sut) {
            let request = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
            let logoField = try sut.inspect().find(NativeTopicImageField.self).actualView()
            logoField.beginUpload(at: url)
            let logoTask = try XCTUnwrap(logoState.uploadTask)
            do {
                _ = try await request.wait()
                XCTAssertTrue(try sut.inspect().find(button: "Create Topic").isDisabled())
                heroState.isUploading = true
                logoField.cancelUpload()
                CannedFeedURLProtocol.releaseResponse(path: path)
                await logoTask.value
                XCTAssertTrue(try sut.inspect().find(button: "Create Topic").isDisabled())
                heroState.isUploading = false
                XCTAssertFalse(try sut.inspect().find(button: "Create Topic").isDisabled())
            } catch {
                logoField.cancelUpload()
                CannedFeedURLProtocol.releaseResponse(path: path)
                await logoTask.value
                throw error
            }
        }
    }

    func testMountedTopicUploadCannotStartWhileSaveRequestIsHeld() async throws {
        let path = "/api/v1/topics"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("web.topics.mutation.default"), 201)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("topic-during-save-\(UUID().uuidString).png")
        try Data("selected-bytes".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        let logoState = NativeTopicImageFieldUploadState()
        let operationState = NativeTopicManagementOperationState()
        let createMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create")?.match)
        let sut = try NativeTopicManagementSurface(
            client: makeClient(), routeMatch: createMatch,
            logoUploadState: logoState, operationState: operationState
        )

        try await ViewHosting.host(sut) {
            let request = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
            try sut.inspect().find(button: "Create Topic").tap()
            XCTAssertTrue(operationState.isSavePending)
            do {
                _ = try await request.wait()
                let field = try sut.inspect().find(NativeTopicImageField.self).actualView()
                XCTAssertTrue(try sut.inspect().find(button: "Upload").isDisabled())
                field.beginUpload(at: url)
                XCTAssertNil(logoState.uploadTask)
                XCTAssertTrue(CannedFeedURLProtocol.capturedRequests
                    .allSatisfy { $0.url.path != "/api/v1/images/upload-url" })
            } catch {
                CannedFeedURLProtocol.releaseResponse(path: path)
                throw error
            }
            CannedFeedURLProtocol.releaseResponse(path: path)
        }
    }

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
            XCTAssertNil(uploadState.localPreviewData)
            XCTAssertNil(try sut.inspect().find(LocalImagePreview.self).actualView().data)

            imageId = "manual-image"
            sut.handleImageIdChange(imageId)
            XCTAssertNil(uploadState.localPreviewData)
            XCTAssertTrue(uploadState.isUploading)
            XCTAssertNil(placement)

            CannedFeedURLProtocol.releaseResponse(path: "/api/v1/images/upload-url")
            await upload.value
            XCTAssertFalse(uploadState.isUploading)
            XCTAssertEqual(imageId, "manual-image")
            XCTAssertNil(uploadState.localPreviewData)
        }
    }

    func testDisappearingCancelsHeldUploadAndDoesNotPermitAnOverlappingUpload() async throws {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("topic-navigation-\(UUID().uuidString).png")
        try Data("selected-bytes".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        let path = "/api/v1/images/upload-url"
        CannedFeedURLProtocol.handlers[path] = (
            Data(
                #"{"upload":{"image_id":"server-image","upload_url":"https://upload.example.test/server-image","content_type":"image/png","expires_at":null}}"#
                    .utf8
            ), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        var imageId = ""
        var placement: ImagePlacement?
        let uploadState = NativeTopicImageFieldUploadState()
        let sut = try NativeTopicImageField(
            title: .nativeSwiftTopicManagementFieldsLogoImage,
            previewWidth: 64,
            imageId: Binding(get: { imageId }, set: { imageId = $0 }),
            placement: Binding(get: { placement }, set: { placement = $0 }),
            client: makeClient(),
            uploadSession: URLSession(configuration: .ephemeral),
            uploadState: uploadState
        )
        try await ViewHosting.host(sut) {
            let request = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
            sut.beginUpload(at: url)
            _ = try await request.wait()
            XCTAssertTrue(uploadState.isUploading)
            XCTAssertNotNil(uploadState.uploadTask)
            try sut.inspect().vStack().callOnDisappear()
            XCTAssertTrue(uploadState.uploadTask?.isCancelled == true)
            if uploadState.isUploading {
                sut.beginUpload(at: url)
                XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
            }
        }
        CannedFeedURLProtocol.releaseResponse(path: path)
        await waitForUploadCompletion(uploadState)
        XCTAssertFalse(uploadState.isUploading)
        XCTAssertNil(uploadState.uploadTask)
        XCTAssertEqual(imageId, "")
        XCTAssertNil(uploadState.localPreviewData)
    }

    func testDisappearingBeforeScheduledUploadStartsDoesNotReadOrBecomeBusy() async throws {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("topic-cancel-before-start-\(UUID().uuidString).png")
        try Data("selected-bytes".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }
        var imageId = ""
        var placement: ImagePlacement?
        let state = NativeTopicImageFieldUploadState()
        let sut = try NativeTopicImageField(
            title: .nativeSwiftTopicManagementFieldsLogoImage,
            previewWidth: 64,
            imageId: Binding(get: { imageId }, set: { imageId = $0 }),
            placement: Binding(get: { placement }, set: { placement = $0 }),
            client: makeClient(),
            uploadState: state
        )
        try await ViewHosting.host(sut) {
            sut.beginUpload(at: url)
            let scheduled = try XCTUnwrap(state.uploadTask)
            try sut.inspect().vStack().callOnDisappear()
            await scheduled.value
            XCTAssertEqual(state.generation, 1, "the canceled task must not start loading after disappearance")
            XCTAssertFalse(state.isUploading)
            XCTAssertNil(state.uploadTask)
            XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.isEmpty)
        }
    }

    private func waitForUploadCompletion(_ state: NativeTopicImageFieldUploadState) async {
        let completed = expectation(description: "cancelled topic upload releases its busy state")
        var active = true
        defer { active = false }
        func observe() {
            guard active else { return }
            if !state.isUploading {
                active = false
                completed.fulfill()
                return
            }
            withObservationTracking {
                _ = state.isUploading
            } onChange: {
                Task { @MainActor in observe() }
            }
        }
        observe()
        await fulfillment(of: [completed], timeout: 2)
    }
}
