import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class ImagePlacementRecoveryTests: NativeRouteSurfaceViewModelTestCase {
    func testCodeOverridesServerCopyForBothErrorRepresentations() {
        for error in [
            VouchaError.api(statusCode: 409, preconditionCode: "IMAGE_PLACEMENT_LIMIT_REACHED"),
            .apiMessage(statusCode: 409, preconditionCode: "IMAGE_PLACEMENT_LIMIT_REACHED", message: "Server copy")
        ] {
            XCTAssertEqual(
                ImagePlacementErrorPresentation.message(for: error),
                .app(UiMessage(.nativeCommonImagePlacementLimitReached))
            )
        }
        XCTAssertNil(ImagePlacementErrorPresentation.message(for: VouchaError.api(
            statusCode: 409,
            preconditionCode: nil
        )))
        XCTAssertNil(ImagePlacementErrorPresentation.message(for: VouchaError.notFound))
    }

    func testComposePreservesDraftAndRetriesAfterCapacityIsFreedOrImageChanges() async throws {
        for community in [nil, "community-1"] as [String?] {
            for replaceImage in [false, true] {
                CannedFeedURLProtocol.reset()
                let model = try NativePostComposeViewModel(client: makeClient(), communityIdOrSlug: community)
                let path = community.map { "/api/v1/communities/\($0)/posts" } ?? "/api/v1/posts"
                CannedFeedURLProtocol.handlers[path] = (
                    ApiFixtureLoader.data("native.posts.images.placement-limit.conflict"),
                    409
                )
                model.title = "Preserved title"
                model.bodyText = "Preserved body"
                model.turnstileToken = "verification"
                model.images = [.init(imageId: "full-image")]
                try model.updateImageCaption(id: XCTUnwrap(model.images.first?.id), caption: "Preserved caption")

                await model.publish()

                guard case let .error(error) = model.state else { return XCTFail("Expected conflict") }
                XCTAssertNotNil(ImagePlacementErrorPresentation.message(for: error))
                XCTAssertEqual(model.title, "Preserved title")
                XCTAssertEqual(model.bodyText, "Preserved body")
                XCTAssertEqual(model.images.first?.caption, "Preserved caption")
                XCTAssertEqual(model.images.first?.imageId, "full-image")
                XCTAssertTrue(model.canPublish)
                XCTAssertNil(model.publishedPostId)
                if replaceImage {
                    try model.removeImage(id: XCTUnwrap(model.images.first?.id))
                    model.images.append(.init(imageId: "other-image"))
                }
                CannedFeedURLProtocol.handlers[path] = try (postEnvelope(), 201)

                await model.publish()

                XCTAssertNotNil(model.publishedPostId)
                XCTAssertTrue(model.images.isEmpty)
                XCTAssertEqual(model.bodyText, "")
            }
        }
    }

    func testTopicSettingsPreservesImagesAndTextAndRetriesWithAnotherImage() async throws {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/about")?.match)
        let model = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: match)
        model.name = "Preserved topic"
        model.markdown = "Preserved topic body"
        model.logoImageId = "full-logo"
        model.heroImageId = "kept-hero"
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1"] = (
            ApiFixtureLoader.data("native.posts.images.placement-limit.conflict"),
            409
        )

        await model.save()

        guard case let .error(error) = model.state else { return XCTFail("Expected conflict") }
        XCTAssertNotNil(ImagePlacementErrorPresentation.message(for: error))
        XCTAssertEqual(model.name, "Preserved topic")
        XCTAssertEqual(model.markdown, "Preserved topic body")
        XCTAssertEqual(model.logoImageId, "full-logo")
        XCTAssertEqual(model.heroImageId, "kept-hero")
        let surface = NativeTopicManagementSurface(client: nil, routeMatch: match)
        XCTAssertNoThrow(try surface.status(viewModel: model).inspect().find(text: UiMessages.string(
            .nativeCommonImagePlacementLimitReached,
            locale: UiLocale.english
        )))
        model.logoImageId = "other-logo"
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1"] = (
            ApiFixtureLoader.data("web.topics.mutation.default"),
            200
        )

        await model.save()

        guard case .loaded = model.state else { return XCTFail("Expected successful retry") }
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last ?? nil)
        let json = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(body.utf8)) as? [String: Any])
        XCTAssertEqual(json["logo_image_id"] as? String, "other-logo")
        XCTAssertEqual(json["hero_image_id"] as? String, "kept-hero")
    }

    func testTopicCreateFollowUpConflictPreservesDraftAndRetriesExistingTopic() async throws {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create")?.match)
        let model = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: match)
        model.name = "Draft name"
        model.markdown = "Draft body"
        model.logoImageId = "full-logo"
        model.heroImageId = "kept-hero"
        let response = ApiFixtureLoader.data("web.topics.mutation.default")
        let json = try XCTUnwrap(JSONSerialization.jsonObject(with: response) as? [String: Any])
        let topic = try XCTUnwrap(json["topic"] as? [String: Any])
        let topicId = try XCTUnwrap(topic["id"] as? String)
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (response, 201)
        CannedFeedURLProtocol.handlers["/api/v1/topics/\(topicId)"] = (
            ApiFixtureLoader.data("native.posts.images.placement-limit.conflict"),
            409
        )

        await model.save()

        XCTAssertEqual(model.name, "Draft name")
        XCTAssertEqual(model.markdown, "Draft body")
        XCTAssertEqual(model.logoImageId, "full-logo")
        XCTAssertEqual(model.heroImageId, "kept-hero")
        XCTAssertEqual(model.topic?.id, topicId)
        guard case .error = model.state else { return XCTFail("Expected conflict") }
        CannedFeedURLProtocol.handlers["/api/v1/topics/\(topicId)"] = (response, 200)

        await model.save()

        guard case .loaded = model.state else { return XCTFail("Expected successful retry") }
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count, 1)
    }

    func testIdentitySettingsPreservesSelectionAndAllowsCapacityRetry() async throws {
        let model = try SettingsViewModel(client: makeClient())
        let response = ApiFixtureLoader.data("swift.my.identity.default")
        model.identity = try APIClient.makeDecoder().decode(IdentityResponse.self, from: response).identity
        model.username = "Preserved username"
        model.profileImageId = "full-avatar"
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            ApiFixtureLoader.data("native.posts.images.placement-limit.conflict"), 409
        )

        await model.saveIdentity()

        XCTAssertEqual(model.username, "Preserved username")
        XCTAssertEqual(model.profileImageId, "full-avatar")
        XCTAssertEqual(model.statusMessage, .app(UiMessage(.nativeCommonImagePlacementLimitReached)))
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (response, 200)

        await model.saveIdentity()

        guard case .loaded = model.state else { return XCTFail("Expected successful retry") }
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.count, 2)
    }

    private func postEnvelope() throws -> Data {
        let data = ApiFixtureLoader.data("swift.posts.feed.default")
        let feed = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let posts = try XCTUnwrap(feed["posts"] as? [String: Any])
        return try JSONSerialization.data(withJSONObject: ["post": XCTUnwrap(posts.values.first)])
    }
}
