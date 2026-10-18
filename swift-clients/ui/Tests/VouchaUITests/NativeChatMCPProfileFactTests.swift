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

        var links = try NativeChatMCPProfileFixture(option: .links)
        links.structured["success"] = false
        XCTAssertNil(try NativeChatMCPFact.makeProfileLinks(links.value))
        links.structured["success"] = true
        var rows = try XCTUnwrap(links.structured["results"] as? [[String: Any]])
        rows[0]["link_type"] = "unsupported"
        links.structured["results"] = rows
        XCTAssertNil(try NativeChatMCPFact.makeProfileLinks(links.value))
        links.structured["results"] = []
        guard case let .profileLinks(empty)? = try NativeChatMCPFact.makeProfileLinks(links.value) else {
            return XCTFail("A successful empty links result remains a distinct fact")
        }
        XCTAssertTrue(empty.isEmpty)
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
}
