@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CRMContactsFormattingTests: XCTestCase {
    func testCrmStatusTitles() {
        XCTAssertEqual(uiEnglish(CrmContactStatus.new.titleKey), "New")
        XCTAssertEqual(uiEnglish(CrmContactStatus.awaitingResponse.titleKey), "Awaiting response")
        XCTAssertEqual(uiEnglish(CrmContactStatus.inConversation.titleKey), "In conversation")
        XCTAssertEqual(uiEnglish(CrmContactStatus.converted.titleKey), "Converted")
        XCTAssertEqual(uiEnglish(CrmContactStatus.archived.titleKey), "Archived")
        XCTAssertEqual(uiEnglish(CrmContactStatus.optedOut.titleKey), "Opted out")
    }

    func testCrmEnumTitles() {
        XCTAssertEqual(uiEnglish(CrmContactVertical.creditCards.titleKey), "Credit cards")
        XCTAssertEqual(uiEnglish(CrmContactType.partner.titleKey), "Partner")
        XCTAssertEqual(uiEnglish(CrmSocialPlatform.xPlatform.titleText), "X")
        XCTAssertEqual(uiEnglish(CrmEmailProvider.ses.titleText), "SES")
        XCTAssertEqual(uiEnglish(CrmEmailProvider.gmailSmtp.titleText), "Gmail SMTP")
    }

    func testLinkedFilterTitles() {
        XCTAssertEqual(uiEnglish(CRMContactsViewModel.LinkedFilter.all.titleKey), "All")
        XCTAssertEqual(uiEnglish(CRMContactsViewModel.LinkedFilter.linked.titleKey), "Linked")
        XCTAssertEqual(uiEnglish(CRMContactsViewModel.LinkedFilter.unlinked.titleKey), "Unlinked")
    }
}
