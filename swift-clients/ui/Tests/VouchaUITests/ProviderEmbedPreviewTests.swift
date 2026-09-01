import ViewInspector
@testable import VouchaDesignSystem
import VouchaModels
import XCTest

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

    private func embed(playerURL: String, sourceURL: String = "https://example.com/source") throws -> UrlEmbed {
        try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "\#(sourceURL)", "title": "oEmbed title", "player_url": "\#(playerURL)" }
        """#.utf8))
    }
}
