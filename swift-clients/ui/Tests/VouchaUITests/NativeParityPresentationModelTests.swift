@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeParityPresentationModelTests: XCTestCase {
    func testNativeTagTabIdentityEqualityAndHashingUseLocalizedKey() {
        let first = NativeTagRelationTab(
            label: UiMessage(.nativeSwiftTagManagementCategoryTopics),
            value: "topic",
            predicate: "category",
            objectType: "topic"
        )
        let equivalent = NativeTagRelationTab(
            label: UiMessage(.nativeSwiftTagManagementCategoryTopics),
            value: "different",
            predicate: "different",
            objectType: "different"
        )
        let distinct = NativeTagRelationTab(
            label: UiMessage(.nativeSwiftTagManagementRelatedPosts),
            value: "post",
            predicate: "related",
            objectType: "post"
        )

        XCTAssertEqual(first.id, .nativeSwiftTagManagementCategoryTopics)
        XCTAssertEqual(first, equivalent)
        XCTAssertNotEqual(first, distinct)
        XCTAssertEqual(Set([first, equivalent, distinct]).count, 2)
    }

    func testNativeChatToolResultsCoverEveryScalarAndCollectionPresentation() {
        let cases: [(DecodedJSONValue, String)] = [
            (.null, "null"),
            (.bool(true), "true"),
            (.number(42), "42.0"),
            (.string("result"), "result"),
            (.array([.null, .null]), "2 items"),
            (.object(["first": .null, "second": .null]), "2 fields")
        ]

        for (index, item) in cases.enumerated() {
            let result = NativeChatToolResult(toolCallId: "tool-\(index)", result: item.0)
            XCTAssertEqual(result.id, "tool-\(index)")
            XCTAssertEqual(uiEnglish(result.displayText), item.1)
        }
    }

    func testModerationReportActionsExposeLocalizedConfirmationTitles() {
        let cases: [(ModerationReportAction, String)] = [
            (.review, "Mark reviewed"),
            (.dismiss, "Dismiss report"),
            (.rerunJudgement, "Re-run judgement"),
            (.warn(reason: "Spam", publicMessage: nil), "Issue warning"),
            (.confirmBanEvasion, "Confirm ban evasion"),
            (.dismissBanEvasion, "Dismiss ban evasion"),
            (.removeTarget, "Remove content")
        ]

        for (action, expectedTitle) in cases {
            XCTAssertEqual(uiEnglish(action.confirmationTitleKey), expectedTitle)
        }
    }

    func testModerationReportErrorMessagesCoverValidationAndFallbackCases() {
        let viewModel = ModerationReportsViewModel(client: nil, viewerTier: .siteModerator)

        XCTAssertEqual(
            uiEnglish(viewModel.message(for: AdminWarningValidationError.reasonTooLong)),
            "Warning reason must be 1000 characters or less."
        )
        XCTAssertEqual(
            uiEnglish(viewModel.message(for: AdminWarningValidationError.publicMessageTooLong)),
            "Public warning message must be 2000 characters or less."
        )
        XCTAssertEqual(
            uiEnglish(viewModel.message(for: PresentationTestFailure())),
            "The action could not be completed."
        )
    }

    func testUrlCrawlPresentationCoversUnavailableStatusAndEveryJsonValueKind() throws {
        let decoder = JSONDecoder()
        let unavailable = try decoder.decode(
            NativeUrlCrawlSummary.self,
            from: Data(#"{"id":"crawl-1"}"#.utf8)
        )
        let available = try decoder.decode(
            NativeUrlCrawlSummary.self,
            from: Data(#"{"id":"crawl-2","responseStatusCode":204}"#.utf8)
        )

        XCTAssertEqual(uiEnglish(unavailable.statusText), "Status unavailable")
        XCTAssertEqual(uiEnglish(available.statusText), "HTTP 204")

        let cases: [(String, String)] = [
            (#""text""#, "text"),
            ("12.5", "12.5"),
            ("true", "True"),
            (#"{"nested":"value"}"#, "Object")
        ]
        for (json, expected) in cases {
            let value = try decoder.decode(NativeJSONValue.self, from: Data(json.utf8))
            XCTAssertEqual(uiEnglish(value.displayText), expected)
        }
    }

    func testPresentationFallbacksCoverLocalizedAndProtocolValues() {
        XCTAssertEqual(
            [ListVisibility.private, .unlisted, .public].map { uiEnglish(listVisibilityText($0)) },
            ["Private", "Unlisted", "Public"]
        )
        XCTAssertEqual(uiEnglish(listItemMediaTypeText(nil)), "Item")
        XCTAssertEqual(uiEnglish(membershipPlanText("enterprise")), "enterprise")
    }
}

private struct PresentationTestFailure: Error {}
