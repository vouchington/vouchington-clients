import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPProfileFactTests: XCTestCase {
    func testCanonicalBioAndLinksBecomeDistinctTypedFacts() throws {
        let bio = try NativeChatMCPProfileFixture(option: .bio)
        guard case let .profileBio(profile)? = try NativeChatMCPFact.makeProfileBio(bio.value) else {
            return XCTFail("Expected canonical bio fact")
        }
        XCTAssertEqual(profile.profileId, "0198ffff-0000-7000-8000-000000000001")
        XCTAssertEqual(profile.markdown, "Example bio")

        let links = try NativeChatMCPProfileFixture(option: .links)
        guard case let .profileLinks(items)? = try NativeChatMCPFact.makeProfileLinks(links.value) else {
            return XCTFail("Expected canonical links fact")
        }
        XCTAssertEqual(items.count, 1)
        XCTAssertEqual(items[0].linkType, .github)
        XCTAssertEqual(items[0].handle, "example")
        XCTAssertEqual(items[0].safeDestination?.absoluteString, "https://github.com/example")
    }

    func testOnlyCompleteSuccessfulSelectedFactsArePublished() throws {
        var bio = try NativeChatMCPProfileFixture(option: .bio)
        bio.structured["success"] = false
        XCTAssertNil(try NativeChatMCPFact.makeProfileBio(bio.value))
        bio.structured["success"] = true
        bio.structured["profile"] = ["id": "profile-only"]
        XCTAssertNil(try NativeChatMCPFact.makeProfileBio(bio.value))
        bio.structured["profile"] = ["id": "profile-only", "markdown": "  \n "]
        XCTAssertNil(try NativeChatMCPFact.makeProfileBio(bio.value))

        var links = try NativeChatMCPProfileFixture(option: .links)
        links.structured["success"] = false
        XCTAssertNil(try NativeChatMCPFact.makeProfileLinks(links.value))
        links.structured["success"] = true
        var rows = try XCTUnwrap(links.structured["results"] as? [[String: Any]])
        rows[0]["link_type"] = "unsupported"
        links.structured["results"] = rows
        XCTAssertNil(try NativeChatMCPFact.makeProfileLinks(links.value))
        links.structured["results"] = []
        XCTAssertNil(try NativeChatMCPFact.makeProfileLinks(links.value))
        let emptyRow: [String: Any] = ["id": "empty", "link_type": "url"]
        links.structured["results"] = [emptyRow]
        XCTAssertNil(try NativeChatMCPFact.makeProfileLinks(links.value))
        links.structured["results"] = [emptyRow, rows[0].merging(["link_type": "github"]) { _, new in new }]
        guard case let .profileLinks(visible)? = try NativeChatMCPFact.makeProfileLinks(links.value) else {
            return XCTFail("Expected nonempty link to remain visible")
        }
        XCTAssertEqual(visible.count, 1)
    }

    func testBioAndLinksUseExistingLocalizedHeadingsWithoutRawEnvelope() throws {
        let bio = try NativeChatMCPProfileFixture(option: .bio)
        let links = try NativeChatMCPProfileFixture(option: .links)
        guard case let .profileBio(profile)? = try NativeChatMCPFact.makeProfileBio(bio.value),
              case let .profileLinks(items)? = try NativeChatMCPFact.makeProfileLinks(links.value) else {
            return XCTFail("Expected canonical profile facts")
        }
        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let bioView = try NativeChatProfileBioFactView(bio: profile, locale: locale)
                .environment(\.locale, locale).inspect()
            XCTAssertNoThrow(try bioView.find(text: UiMessages.string(.nativeSwiftProfileBio, locale: locale)))
            XCTAssertNoThrow(try bioView.find(text: "Example bio"))
            XCTAssertThrowsError(try bioView
                .find(text: String(data: bio.value.exactJSONData(), encoding: .utf8) ?? ""))

            let linksView = try NativeChatProfileLinksFactView(links: items, locale: locale).inspect()
            XCTAssertNoThrow(try linksView.find(text: UiMessages.string(
                .nativeSwiftSettingsProfileLinks,
                locale: locale
            )))
            XCTAssertNoThrow(try linksView.find(text: UiMessages.string(
                .nativeSwiftPresentationValuesGithub,
                locale: locale
            )))
            XCTAssertNoThrow(try linksView.find(text: "example"))
            XCTAssertEqual(linksView.findAll(ViewType.Link.self).count, 1)
            XCTAssertThrowsError(try linksView.find(text: String(
                data: links.value.exactJSONData(),
                encoding: .utf8
            ) ?? ""))
        }
    }

    func testUnsafeURLRemainsTextAndCannotBecomeAChatLink() throws {
        let bad = NativeChatProfileLinkFact(
            id: "link-1", linkType: .url, handle: nil, name: "Untrusted label", url: "javascript:alert(1)"
        )
        XCTAssertNil(bad.safeDestination)
        let inspected = try NativeChatProfileLinksFactView(
            links: [bad], locale: Locale(identifier: "en")
        ).inspect()
        XCTAssertTrue(inspected.findAll(ViewType.Link.self).isEmpty)
        XCTAssertNoThrow(try inspected.find(text: "Untrusted label"))
    }

    func testURLOnlyLinksShowRejectedAddressAsTextButKeepSafeAddressClickable() throws {
        let rejected = NativeChatProfileLinkFact(
            id: "unsafe", linkType: .url, handle: nil, name: nil,
            url: "http://example.com/profile"
        )
        let safe = NativeChatProfileLinkFact(
            id: "safe", linkType: .url, handle: nil, name: nil,
            url: "https://example.com/profile"
        )
        let inspected = try NativeChatProfileLinksFactView(
            links: [rejected, safe], locale: Locale(identifier: "en")
        ).inspect()

        XCTAssertNil(rejected.safeDestination)
        XCTAssertEqual(inspected.findAll(ViewType.Link.self).count, 1)
        XCTAssertNoThrow(try inspected.find(text: "http://example.com/profile"))
        XCTAssertNoThrow(try inspected.find(text: "example.com"))
    }

    func testWhitespaceOnlyNamesDoNotHideLinkDestinations() throws {
        let rejected = NativeChatProfileLinkFact(
            id: "unsafe", linkType: .url, handle: nil, name: " \n ",
            url: "http://example.com/profile"
        )
        let safe = NativeChatProfileLinkFact(
            id: "safe", linkType: .url, handle: nil, name: " \n ",
            url: "https://example.com/profile"
        )
        let inspected = try NativeChatProfileLinksFactView(
            links: [rejected, safe], locale: Locale(identifier: "en")
        ).inspect()

        XCTAssertEqual(inspected.findAll(ViewType.Link.self).count, 1)
        XCTAssertNoThrow(try inspected.find(text: "http://example.com/profile"))
        XCTAssertNoThrow(try inspected.find(text: "example.com"))
    }

    func testWhitespaceOnlyHandleUsesSavedURLInsteadOfConstructedProfileURL() {
        let saved = NativeChatProfileLinkFact(
            id: "saved", linkType: .github, handle: " \n ", name: nil,
            url: "https://example.com/saved-profile"
        )
        let missing = NativeChatProfileLinkFact(
            id: "missing", linkType: .github, handle: " \n ", name: nil, url: nil
        )

        XCTAssertEqual(saved.safeDestination?.absoluteString, "https://example.com/saved-profile")
        XCTAssertNil(missing.safeDestination)
    }
}
