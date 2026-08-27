import SwiftUI
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeHtmlContentTests: XCTestCase {
    func testContentViewRendersAttributedText() throws {
        let sut = NativeHtmlContent(html: "<p>Rendered <strong>body</strong></p>")

        XCTAssertEqual(try sut.inspect().find(ViewType.Text.self).string(), "Rendered body\n\n")
    }

    func testContentViewOmitsBlankContentWithoutFallback() throws {
        let sut = NativeHtmlContent(html: "   ")

        XCTAssertTrue(try sut.inspect().findAll(ViewType.Text.self).isEmpty)
    }

    func testPlainTextDropsUnsafeTagsAndPreservesSafeText() {
        let text = NativeHtmlContent.plainText(
            html: "<p>Hello <strong>world</strong><script>alert(1)</script></p>",
            fallback: nil
        )

        XCTAssertTrue(text.contains("Hello"))
        XCTAssertTrue(text.contains("world"))
        XCTAssertFalse(text.contains("alert"))
    }

    func testPlainTextPreservesListText() {
        let text = NativeHtmlContent.plainText(
            html: "<ol><li>First</li><li>Second</li></ol>",
            fallback: nil
        )

        XCTAssertTrue(text.contains("1. First"))
        XCTAssertTrue(text.contains("2. Second"))
    }

    func testPlainTextPreservesNestedListBoundaries() {
        let text = NativeHtmlContent.plainText(
            html: "<ul><li>Parent<ul><li>Child</li></ul></li></ul>",
            fallback: nil
        )

        XCTAssertTrue(text.contains("- Parent\n- Child"))
    }

    func testPlainTextPreservesTableCellBoundaries() {
        let text = NativeHtmlContent.plainText(
            html: "<table><tr><th>Name</th><th>Status</th></tr><tr><td>Ada</td><td>Active</td></tr></table>",
            fallback: nil
        )

        XCTAssertTrue(text.contains("Name | Status"))
        XCTAssertTrue(text.contains("Ada | Active"))
    }

    func testPlainTextUsesFallbackForBlankHtml() {
        XCTAssertEqual(NativeHtmlContent.plainText(html: "", fallback: "raw markdown"), "raw markdown")
    }

    func testPlainTextUsesFallbackWhenHtmlRendersEmpty() {
        XCTAssertEqual(
            NativeHtmlContent.plainText(html: "<script>alert(1)</script>", fallback: "raw markdown"),
            "raw markdown"
        )
    }

    func testPlainTextPreservesPreformattedWhitespace() {
        XCTAssertEqual(
            NativeHtmlContent.plainText(html: "<pre><code>let x = 1\n  let y = 2</code></pre>", fallback: nil),
            "let x = 1\n  let y = 2\n\n"
        )
    }

    func testMissingImageAltUsesTheActiveUiLocale() {
        let expectations = [
            ("en", "[Image]"),
            ("es", "[Imagen]"),
            ("fr", "[Image]"),
            ("pt", "[Imagem]")
        ]

        for (localeIdentifier, expected) in expectations {
            XCTAssertEqual(
                NativeHtmlContent.plainText(
                    html: "<img>",
                    locale: Locale(identifier: localeIdentifier)
                ),
                expected
            )
        }
    }

    func testExplicitImageAltRemainsExternalContentAcrossUiLocales() {
        for localeIdentifier in ["en", "es", "fr", "pt"] {
            XCTAssertEqual(
                NativeHtmlContent.plainText(
                    html: "<img alt=\"Voucha logo\">",
                    locale: Locale(identifier: localeIdentifier)
                ),
                "[Voucha logo]"
            )
        }
    }

    func testPlainTextCoversHtmlBlockVariants() {
        let text = NativeHtmlContent.plainText(
            html: """
            <h2>Heading</h2>
            <blockquote>Quoted<br>line</blockquote>
            <pre>let x = 1</pre>
            <p><em>soft</em> <code>code</code> <a href="/x">link</a> <img alt="Logo"></p>
            <hr>
            <section><span>Nested text</span></section>
            """,
            fallback: nil
        )

        XCTAssertTrue(text.contains("Heading"))
        XCTAssertTrue(text.contains("> Quoted"))
        XCTAssertTrue(text.contains("> line"))
        XCTAssertTrue(text.contains("let x = 1"))
        XCTAssertTrue(text.contains("soft code link [Logo]"))
        XCTAssertTrue(text.contains("---"))
        XCTAssertTrue(text.contains("Nested text"))
    }

    func testAttributedTextAppliesInlineAttributes() throws {
        let attributed = NativeHtmlText.attributedText(
            html: "<p><strong>Strong</strong> <em>Soft</em> <code>Code</code> <a href=\"https://example.test\">Link</a></p>"
        )
        let string = String(attributed.characters)

        let strongRun = try XCTUnwrap(attributed.runs.first { String(attributed[$0.range].characters) == "Strong" })
        let emphasisRun = try XCTUnwrap(attributed.runs.first { String(attributed[$0.range].characters) == "Soft" })
        let codeRun = try XCTUnwrap(attributed.runs.first { String(attributed[$0.range].characters) == "Code" })
        let linkRun = try XCTUnwrap(attributed.runs.first { String(attributed[$0.range].characters) == "Link" })

        XCTAssertEqual(string, "Strong Soft Code Link\n\n")
        XCTAssertEqual(strongRun.inlinePresentationIntent, .stronglyEmphasized)
        XCTAssertEqual(emphasisRun.inlinePresentationIntent, .emphasized)
        XCTAssertEqual(codeRun.inlinePresentationIntent, .code)
        XCTAssertEqual(linkRun.link, URL(string: "https://example.test"))
    }

    func testAttributedTextMergesNestedInlineAttributes() throws {
        let attributed = NativeHtmlText.attributedText(html: "<p><strong><em>Both</em></strong></p>")
        let run = try XCTUnwrap(attributed.runs.first { String(attributed[$0.range].characters) == "Both" })

        XCTAssertTrue(run.inlinePresentationIntent?.contains(.stronglyEmphasized) == true)
        XCTAssertTrue(run.inlinePresentationIntent?.contains(.emphasized) == true)
    }

    func testBlockquotePrefixPreservesInlineAttributes() throws {
        let attributed = NativeHtmlText.attributedText(html: "<blockquote><strong>Quoted</strong></blockquote>")
        let run = try XCTUnwrap(attributed.runs.first { String(attributed[$0.range].characters) == "Quoted" })

        XCTAssertEqual(String(attributed.characters), "> Quoted\n\n")
        XCTAssertEqual(run.inlinePresentationIntent, .stronglyEmphasized)
    }

    func testRelativeLinksRenderWithoutSystemLinkAttribute() {
        let attributed = NativeHtmlText.attributedText(
            html: "<p><a href=\"/discussion/abc/comment/def\">Comment</a></p>"
        )

        XCTAssertEqual(String(attributed.characters), "Comment\n\n")
        XCTAssertFalse(attributed.runs.contains { $0.link != nil })
    }

    func testUnsafeLinkSchemesRenderWithoutSystemLinkAttribute() {
        let attributed = NativeHtmlText.attributedText(html: "<p><a href=\"javascript:alert(1)\">Unsafe</a></p>")

        XCTAssertEqual(String(attributed.characters), "Unsafe\n\n")
        XCTAssertFalse(attributed.runs.contains { $0.link != nil })
    }

    func testMarkdownAutocompleteTokenParsesLastToken() throws {
        let markdown = "hello @jon"
        let token = try XCTUnwrap(MarkdownAutocompleteToken.parse(markdown))

        XCTAssertEqual(token.kind, .user)
        XCTAssertEqual(token.query, "jon")
        XCTAssertEqual(String(markdown[token.range]), "@jon")
    }

    func testMarkdownAutocompleteTokenParsesTopicAndPostMarkers() {
        XCTAssertEqual(MarkdownAutocompleteToken.parse("#swift")?.kind, .topic)
        XCTAssertEqual(MarkdownAutocompleteToken.parse("see !post-1")?.kind, .post)
    }

    func testMarkdownAutocompleteTokenRejectsEmptyOrUnknownMarkers() {
        XCTAssertNil(MarkdownAutocompleteToken.parse(""))
        XCTAssertNil(MarkdownAutocompleteToken.parse("@"))
        XCTAssertNil(MarkdownAutocompleteToken.parse("hello"))
        XCTAssertNil(MarkdownAutocompleteToken.parse("hello @"))
    }

    func testMarkdownEditorRendersWriteMode() throws {
        var value = "hello @jo"
        let binding = Binding(get: { value }, set: { value = $0 })
        let sut = NativeMarkdownEditor(client: nil, markdown: binding)

        let inspected = try sut.inspect()
        XCTAssertNoThrow(try inspected.find(ViewType.Picker.self))
        XCTAssertNoThrow(try inspected.find(ViewType.TextEditor.self))
    }

    func testMarkdownEditorRendersSuggestionDropdown() throws {
        var value = "hello @jo"
        let binding = Binding(get: { value }, set: { value = $0 })
        let sut = NativeMarkdownEditor(
            client: nil,
            markdown: binding,
            initialSuggestions: [
                .init(replacement: "@jonathan", label: "@jonathan", detail: "Jonathan")
            ]
        )
        let inspected = try sut.inspect()

        XCTAssertEqual(try inspected.find(text: "@jonathan").string(), "@jonathan")
        XCTAssertEqual(try inspected.find(text: "Jonathan").string(), "Jonathan")
    }

    func testMarkdownEditorPreviewStatesRenderNativeContent() throws {
        var empty = ""
        var markdown = "**Preview**"
        let emptyPreview = NativeMarkdownEditor(
            client: nil,
            markdown: Binding(get: { empty }, set: { empty = $0 }),
            initialMode: .preview
        )
        let loadedPreview = NativeMarkdownEditor(
            client: nil,
            markdown: Binding(get: { markdown }, set: { markdown = $0 }),
            initialMode: .preview,
            initialPreviewHtml: "<p>Preview</p>",
            initialPreviewState: .loaded
        )
        let loadingPreview = NativeMarkdownEditor(
            client: nil,
            markdown: Binding(get: { markdown }, set: { markdown = $0 }),
            initialMode: .preview,
            initialPreviewState: .loading
        )
        let errorPreview = NativeMarkdownEditor(
            client: nil,
            markdown: Binding(get: { markdown }, set: { markdown = $0 }),
            initialMode: .preview,
            initialPreviewState: .error(.api(statusCode: 500, preconditionCode: nil))
        )

        XCTAssertEqual(
            try emptyPreview.inspect().find(text: "Nothing to preview yet.").string(),
            "Nothing to preview yet."
        )
        XCTAssertEqual(try loadedPreview.inspect().find(text: "Preview\n\n").string(), "Preview\n\n")
        XCTAssertNoThrow(try loadingPreview.inspect().find(ViewType.ProgressView.self))
        XCTAssertEqual(try errorPreview.inspect().find(text: "Preview unavailable.").string(), "Preview unavailable.")
    }

    func testMarkdownEditorAutocompleteHelpersWithoutClient() async throws {
        var value = "hello @jo"
        let binding = Binding(get: { value }, set: { value = $0 })
        let sut = NativeMarkdownEditor(client: nil, markdown: binding)
        let token = try XCTUnwrap(MarkdownAutocompleteToken.parse(value))
        let suggestions = await sut.loadSuggestions(for: token)
        let inserted = NativeMarkdownEditor.insertedMarkdown(
            value,
            suggestion: .init(replacement: "@jonathan", label: "@jonathan", detail: nil),
            token: token
        )

        XCTAssertEqual(suggestions, [])
        XCTAssertEqual(inserted, "hello @jonathan ")
    }

    func testMarkdownEditorRefreshPreviewRequestsBackendHtml() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/markdown/preview": (Data(#"{"html":"<p>Preview</p>"}"#.utf8), 200)
        ]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
        var value = "**Preview**"
        let sut = try NativeMarkdownEditor(
            client: makeMarkdownClient(),
            markdown: Binding(get: { value }, set: { value = $0 })
        )

        await sut.refreshPreview()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/markdown/preview")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.first, "POST")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.first??.contains("**Preview**") == true)
    }

    func testMarkdownEditorRefreshPreviewHandlesBlankNilClientAndServerErrors() async throws {
        var blank = " "
        var markdown = "**Preview**"
        let blankEditor = NativeMarkdownEditor(
            client: nil,
            markdown: Binding(get: { blank }, set: { blank = $0 })
        )
        let nilClientEditor = NativeMarkdownEditor(
            client: nil,
            markdown: Binding(get: { markdown }, set: { markdown = $0 })
        )
        CannedFeedURLProtocol.handlers = [
            "/api/v1/markdown/preview": (Data(#"{"error":"nope"}"#.utf8), 500)
        ]
        let failingEditor = try NativeMarkdownEditor(
            client: makeMarkdownClient(),
            markdown: Binding(get: { markdown }, set: { markdown = $0 })
        )

        await blankEditor.refreshPreview()
        await nilClientEditor.refreshPreview()
        await failingEditor.refreshPreview()
    }

    func testMarkdownAutocompleteLoadsUsersTopicsAndPosts() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/users": (
                Data(
                    #"{"results":[{"id":"user-1","username":"jonathan","name":"Jonathan","roles":[]}],"page_info":{"has_next_page":false}}"#
                        .utf8
                ),
                200
            ),
            "/api/v1/topics": (
                Data(
                    #"{"results":[{"__entity_type":"topic","id":"topic-1","name":"Swift","slug":"swift","topic_type":"topic","hostname_id":null,"hostname":null,"logo_image_id":null,"hero_image_id":null,"homepage_url_id":null,"lingua_rs_detected_language":null,"referral_program_id":null,"referral_program_slug":null,"rewards_program_id":null},{"__entity_type":"topic","id":"topic-2","name":"Missing"}],"topics":{},"page_info":{"has_next_page":false}}"#
                        .utf8
                ),
                200
            ),
            "/api/v1/posts": (
                Data(
                    #"{"results":[{"id":"result-1","entity_id":"post-1"},{"id":"missing"}],"posts":{"post-1":{"id":"post-1","slug":"native-markdown","post_type":"discussion","title":"Native Markdown","markdown":"body","parent_id":null,"root_id":null,"created_by_id":"user-1","created_at":"2026-07-09T00:00:00Z","broadcast":"everyone","privacy":"public","is_anonymous":false,"community_id":null,"clearance_status":null,"deleted_at":null,"deleted_by_id":null,"locked_at":null,"locked_by_id":null,"archived_at":null,"archived_by_id":null,"clearance_reason":null,"clearance_updated_at":null,"spam_detection_created_at":null,"spam_detection_flagged":null,"spam_detection_results":null,"spam_detection_score":null,"updated_by_id":null}}}"#
                        .utf8
                ),
                200
            )
        ]
        let sut = try NativeMarkdownEditor(client: makeMarkdownClient(), markdown: .constant(""))

        let users = try await sut.loadSuggestions(for: XCTUnwrap(MarkdownAutocompleteToken.parse("@jon")))
        let topics = try await sut.loadSuggestions(for: XCTUnwrap(MarkdownAutocompleteToken.parse("#swi")))
        let posts = try await sut.loadSuggestions(for: XCTUnwrap(MarkdownAutocompleteToken.parse("!nat")))

        XCTAssertEqual(
            users,
            [MarkdownAutocompleteSuggestion(replacement: "@jonathan", label: "@jonathan", detail: "Jonathan")]
        )
        XCTAssertEqual(
            topics,
            [MarkdownAutocompleteSuggestion(replacement: "#swift", label: "#swift", detail: "Swift")]
        )
        XCTAssertEqual(
            posts,
            [
                MarkdownAutocompleteSuggestion(
                    replacement: "!native-markdown",
                    label: "!native-markdown",
                    detail: "Native Markdown"
                )
            ]
        )
    }

    func testMarkdownAutocompleteFollowsCursorToFillPage() async throws {
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (userAutocompletePage(id: "user-1", username: "jonathan", cursor: "next", hasMore: true), 200, 0),
            (userAutocompletePage(id: "user-2", username: "jonas"), 200, 0)
        ]
        let sut = try NativeMarkdownEditor(client: makeMarkdownClient(), markdown: .constant(""))

        let suggestions = try await sut.loadSuggestions(for: XCTUnwrap(MarkdownAutocompleteToken.parse("@jon")))

        XCTAssertEqual(suggestions.map(\.replacement), ["@jonathan", "@jonas"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users" }.count, 2)
    }

    func testMarkdownAutocompleteStopsFollowingCursorAtSafetyBound() async throws {
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = (0 ..< 5).map { page in
            (emptyUserAutocompletePage(cursor: "c-\(page)"), 200, 0)
        }
        let sut = try NativeMarkdownEditor(client: makeMarkdownClient(), markdown: .constant(""))

        let suggestions = try await sut.loadSuggestions(for: XCTUnwrap(MarkdownAutocompleteToken.parse("@jon")))

        XCTAssertTrue(suggestions.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/users" }.count, 5)
    }

    private func userAutocompletePage(
        id: String,
        username: String,
        cursor: String? = nil,
        hasMore: Bool = false
    ) -> Data {
        let endCursor = cursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            #"""
            {"results":[{"id":"\#(id)","username":"\#(username)","roles":[]}],
            "page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(endCursor)}}
            """#.utf8
        )
    }

    private func emptyUserAutocompletePage(cursor: String) -> Data {
        Data(
            #"""
            {"results":[],"page_info":{"has_next_page":true,"end_cursor":"\#(cursor)"}}
            """#.utf8
        )
    }

    private func makeMarkdownClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://api.test"))),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }
}
