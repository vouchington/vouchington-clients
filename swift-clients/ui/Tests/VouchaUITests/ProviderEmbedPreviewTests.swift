import SwiftUI
import ViewInspector
@testable import VouchaDesignSystem
import VouchaModels
import XCTest

extension ProviderEmbedPlayerSheet: PopupPresenter {
    public var onDismiss: (() -> Void)? {
        nil
    }
}

@MainActor
final class ProviderEmbedPreviewTests: XCTestCase {
    func testRendersServerTitleAndAlwaysVisibleSourceActionBeforePlayback() throws {
        let sut = try ProviderEmbedPreview(embed: embed(playerURL: "https://www.youtube-nocookie.com/embed/video-123"))

        XCTAssertNoThrow(try sut.inspect().find(text: "oEmbed title"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Play video"))
        XCTAssertNoThrow(try sut.inspect().find(ViewType.Link.self))

    }

    func testDoesNotOfferPlayerForUnapprovedServerURL() throws {
        let sut = try ProviderEmbedPreview(embed: embed(playerURL: "https://example.com/embed/video-123"))

        XCTAssertThrowsError(try sut.inspect().find(text: "Play video"))
        XCTAssertNoThrow(try sut.inspect().find(ViewType.Link.self))
    }

    func testDoesNotRenderAnInsecureSourceLink() throws {
        let sut = try ProviderEmbedPreview(embed: embed(
            playerURL: "https://www.youtube-nocookie.com/embed/video-123",
            sourceURL: "http://example.com/source"
        ))

        XCTAssertThrowsError(try sut.inspect().find(ViewType.Link.self))
        XCTAssertThrowsError(try sut.inspect().find(text: "Play video"))
    }

    func testDoesNotOfferPlayerWithoutSourceAction() throws {
        let sut = try ProviderEmbedPreview(embed: embed(
            playerURL: "https://www.youtube-nocookie.com/embed/video-123",
            sourceURL: ""
        ))

        XCTAssertThrowsError(try sut.inspect().find(text: "Play video"))
        XCTAssertThrowsError(try sut.inspect().find(ViewType.Link.self))
    }

    func testRendersSafeThumbnailAndDescription() throws {
        let embed = try JSONDecoder.vouchaFixtureDecoder.decode(
            UrlEmbed.self,
            from: Data(#"""
            {
              "source_url": "https://example.com/source",
              "title": "oEmbed title",
              "description": "Safe oEmbed description",
              "thumbnail_url": "https://images.example/thumbnail.jpg"
            }
            """#.utf8)
        )
        let sut = ProviderEmbedPreview(embed: embed)

        let thumbnail = try sut.inspect().find(ViewType.View<AsyncImageView>.self).actualView()

        XCTAssertEqual(thumbnail.resolvedURLString, "https://images.example/thumbnail.jpg")
        XCTAssertNoThrow(try sut.inspect().find(text: "Safe oEmbed description"))
    }

    func testApprovedPlayerActionBuildsApprovedPlayerSourceContent() async throws {
        let preview = try ProviderEmbedPreview(embed: embed(
            playerURL: "https://www.youtube-nocookie.com/embed/video-123"
        ))

        try await ViewHosting.host(preview) {
            let playButton = try preview.inspect().find(button: "Play video")
            try playButton.tap()
            await Task.yield()

            let playerSheet = try playButton.modifier(ProviderEmbedPlayerSheet.self).actualView()
            let presentedSheet = try playerSheet.popupBuilder().inspect().anyView()
            XCTAssertNoThrow(try presentedSheet.find(viewWithAccessibilityIdentifier: "provider-embed-player"))
            let links = presentedSheet.findAll(ViewType.Link.self)
            XCTAssertEqual(links.count, 1)
            XCTAssertEqual(try XCTUnwrap(links.first).url(), URL(string: "https://example.com/source"))
            XCTAssertNoThrow(try presentedSheet.find(text: "Open source"))
        }
    }

    private func embed(playerURL: String, sourceURL: String = "https://example.com/source") throws -> UrlEmbed {
        try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "\#(sourceURL)", "title": "oEmbed title", "player_url": "\#(playerURL)" }
        """#.utf8))
    }
}
