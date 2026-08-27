import Foundation
@testable import VouchaModels
import XCTest

final class CrmModelDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let string = try container.decode(String.self)
            if let date = VouchaDateParser.parse(string) {
                return date
            }
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot parse date: \(string)")
        }
        return decoder
    }()

    func testDecodesCrmContactListFixture() throws {
        let page = try decoder.decode(Page<CrmContact>.self, from: ApiFixtureLoader.data("native.crm.contacts.default"))
        XCTAssertEqual(page.results.count, 1)
        let contact = try XCTUnwrap(page.results.first)
        XCTAssertEqual(contact.id, "00000000-0000-7000-8000-000000000584")
        XCTAssertEqual(contact.status, .new)
        XCTAssertEqual(contact.vertical, .creditCards)
    }

    func testDecodesCrmContactDetailFixture() throws {
        let response = try decoder.decode(
            CrmContactDetailResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-detail.default")
        )
        XCTAssertEqual(response.contact.email, "alice@example.test")
        XCTAssertEqual(response.socialAccounts.count, 1)
        XCTAssertEqual(response.socialAccounts.first?.platform, .instagram)
    }

    func testDecodesCrmContactMutationResponses() throws {
        let create = try decoder.decode(
            CrmContactResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-create.default")
        )
        XCTAssertEqual(create.contact.phone, nil)

        let update = try decoder.decode(
            CrmContactResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-update.default")
        )
        XCTAssertEqual(update.contact.phone, "+1-415-555-0100")

        let link = try decoder.decode(
            CrmContactResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-link-user.default")
        )
        XCTAssertEqual(link.contact.userId, "00000000-0000-7000-8000-000000000001")

        let unlink = try decoder.decode(
            CrmContactResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-unlink-user.default")
        )
        XCTAssertNil(unlink.contact.userId)
    }

    func testDecodesCrmMessageAndNoteFixtures() throws {
        let emails = try decoder.decode(
            Page<CrmMessage>.self,
            from: ApiFixtureLoader.data("native.crm.contact-emails.default")
        )
        XCTAssertEqual(emails.results.first?.emailProvider, .ses)
        XCTAssertEqual(emails.results.first?.direction, .outbound)

        let send = try decoder.decode(
            CrmMessageResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-email-send.default")
        )
        XCTAssertEqual(send.message.subject, "Warm intro")

        let notes = try decoder.decode(
            Page<CrmNote>.self,
            from: ApiFixtureLoader.data("native.crm.contact-notes.default")
        )
        XCTAssertEqual(notes.results.first?.body, "Met at the conference")

        let note = try decoder.decode(
            CrmNoteResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-note-create.default")
        )
        XCTAssertEqual(note.note.conversationId, "00000000-0000-7000-8000-000000000700")
    }

    func testDecodesCrmEmailDraftAndImportFixtures() throws {
        let draft = try decoder.decode(
            CrmEmailDraftResponse.self,
            from: ApiFixtureLoader.data("native.crm.contact-email-draft.default")
        )
        XCTAssertEqual(draft.draft.subject, "Warm intro")
        XCTAssertEqual(draft.draft.bodyText, "Hi Alice, it was great to see your recent creator work.")

        let success = try decoder.decode(
            CrmImportBatchResponse.self,
            from: ApiFixtureLoader.data("native.crm.import.success.default")
        )
        XCTAssertTrue(success.valid)
        XCTAssertEqual(success.batch?.importType, "crm_contact")

        let validation = try decoder.decode(
            CrmImportBatchResponse.self,
            from: ApiFixtureLoader.data("native.crm.import.validation.default")
        )
        XCTAssertFalse(validation.valid)
        XCTAssertEqual(validation.validation?.rows.first?.errors.first, "follower_count must be a non-negative integer")
    }
}

private enum VouchaDateParser {
    private static let iso8601WithFractionalSeconds = Date.ISO8601FormatStyle(includingFractionalSeconds: true)
    private static let iso8601 = Date.ISO8601FormatStyle(includingFractionalSeconds: false)

    static func parse(_ string: String) -> Date? {
        if let date = try? Date(string, strategy: iso8601WithFractionalSeconds) {
            return date
        }
        return try? Date(string, strategy: iso8601)
    }
}
