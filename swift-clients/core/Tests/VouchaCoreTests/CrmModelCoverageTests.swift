import Foundation
@testable import VouchaModels
import XCTest

final class CrmModelCoverageTests: XCTestCase {
    func testDecodesCrmEnumsFromRawValues() throws {
        let decoder = makeVouchaDecoder()

        XCTAssertEqual(try decoder.decode(CrmContactType.self, from: Data("\"customer\"".utf8)), .customer)
        XCTAssertEqual(try decoder.decode(CrmContactSource.self, from: Data("\"csv_import\"".utf8)), .csvImport)
        XCTAssertEqual(try decoder.decode(CrmContactVertical.self, from: Data("\"ai\"".utf8)), .artificialIntelligence)
        XCTAssertEqual(try decoder.decode(CrmContactVertical.self, from: Data("\"gaming\"".utf8)), .other)
        XCTAssertEqual(try decoder.decode(CrmSocialPlatform.self, from: Data("\"x\"".utf8)), .xPlatform)
        XCTAssertEqual(try decoder.decode(CrmContactStatus.self, from: Data("\"opted_out\"".utf8)), .optedOut)
        XCTAssertEqual(try decoder.decode(CrmMessageDirection.self, from: Data("\"inbound\"".utf8)), .inbound)
        XCTAssertEqual(try decoder.decode(CrmEmailProvider.self, from: Data("\"gmail_smtp\"".utf8)), .gmailSmtp)
    }

    func testCrmContactStatusPriority() {
        let baseDate = Date(timeIntervalSince1970: 1_700_000_000)

        XCTAssertEqual(contact(id: "new", at: baseDate).status, .new)
        XCTAssertEqual(contact(id: "contacted", at: baseDate, contactedAt: baseDate).status, .awaitingResponse)
        XCTAssertEqual(contact(id: "responded", at: baseDate, respondedAt: baseDate).status, .inConversation)
        XCTAssertEqual(contact(id: "converted", at: baseDate, convertedAt: baseDate).status, .converted)
        XCTAssertEqual(contact(id: "archived", at: baseDate, archivedAt: baseDate).status, .archived)
        XCTAssertEqual(
            contact(id: "opted-out", at: baseDate, optedOutAt: baseDate, archivedAt: baseDate).status,
            .optedOut
        )
    }

    private func contact(
        id: String,
        at date: Date,
        contactedAt: Date? = nil,
        respondedAt: Date? = nil,
        convertedAt: Date? = nil,
        optedOutAt: Date? = nil,
        archivedAt: Date? = nil
    ) -> CrmContact {
        CrmContact(
            id: "contact-\(id)",
            name: id,
            email: "\(id)@example.test",
            contactedAt: contactedAt,
            respondedAt: respondedAt,
            convertedAt: convertedAt,
            optedOutAt: optedOutAt,
            archivedAt: archivedAt,
            createdAt: date,
            updatedAt: date
        )
    }
}
