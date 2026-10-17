import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativePostAuthorBioTests: NativeRouteSurfaceViewModelTestCase {
    func testKnownLanguagesApplyOnlyToBio() throws {
        for (language, direction) in [("en", LayoutDirection.leftToRight), ("ar", .rightToLeft)] {
            let aside = try decodeAside(language: language)
            let bio = NativePostAuthorBio(aside: aside)
            let text = try bio.inspect().find(ViewType.Text.self)
            XCTAssertEqual(try text.string(), "Biography\n\n")
            XCTAssertEqual(try text.environment(\.layoutDirection), direction)
            XCTAssertEqual(try text.environment(\.locale).identifier, language)
        }
    }

    func testUnknownLanguageDoesNotOverrideLeafEnvironment() throws {
        for language in [nil, "invalid"] as [String?] {
            let bio = try NativePostAuthorBio(aside: decodeAside(language: language))
            let text = try bio.inspect().find(ViewType.Text.self)
            XCTAssertThrowsError(try text.environment(\.layoutDirection))
        }
    }

    func testAbsentAsideRendersNoBiography() throws {
        XCTAssertThrowsError(try NativePostAuthorBio(aside: nil).inspect().find(ViewType.Text.self))
    }

    func testThreadRetainsBioOnFailedReloadAndClearsItForAnonymousAuthor() async throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        var json = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.comments.post-detail.default")
        ) as? [String: Any])
        json["author_aside"] = [
            "about_html": "<p>Biography</p>",
            "is_following": false,
            "profile_links": [],
            "lingua_rs_detected_language": "ar"
        ] as [String: Any]
        let model = try NativeCommentThreadViewModel(client: makeClient(), rootPostId: "comment-root-post")
        try model.apply(detailResponse: decoder.decode(
            PostEnvelope.self,
            from: JSONSerialization.data(withJSONObject: json)
        ))
        XCTAssertEqual(model.authorAside?.linguaRsDetectedLanguage, "ar")
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-root-post"] = (
            Data(#"{"error":"unavailable"}"#.utf8), 500
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-root-post/descendants"] = (
            Data(#"{"error":"unavailable"}"#.utf8), 500
        )
        await model.loadThread()
        XCTAssertEqual(model.authorAside?.linguaRsDetectedLanguage, "ar")
        var post = try XCTUnwrap(json["post"] as? [String: Any])
        post["is_anonymous"] = true
        json["post"] = post
        try model.apply(detailResponse: decoder.decode(
            PostEnvelope.self,
            from: JSONSerialization.data(withJSONObject: json)
        ))
        XCTAssertNil(model.authorAside)
    }

    private func decodeAside(language: String?) throws -> PostAuthorAside {
        let json: [String: Any] = [
            "about_html": "<p>Biography</p>",
            "is_following": false,
            "profile_links": [],
            "lingua_rs_detected_language": language as Any? ?? NSNull()
        ]
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(PostAuthorAside.self, from: JSONSerialization.data(withJSONObject: json))
    }
}
