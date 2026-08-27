import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointCRMTimestampTests: XCTestCase {
    func testSendCrmEmailEncodesAiGeneratedTimestamp() {
        assertEndpoint(
            Endpoint.sendCrmEmail(
                contactId: "contact 1",
                subject: "Warm intro",
                bodyText: "Hello",
                emailProvider: .ses,
                aiPrompt: "Write a warm intro",
                aiGeneratedAt: Date(timeIntervalSince1970: 1_688_212_370)
            ),
            method: .POST,
            path: "/api/v1/crm/contacts/contact%201/emails",
            body: [
                "subject": "Warm intro",
                "body_text": "Hello",
                "email_provider": "ses",
                "ai_prompt": "Write a warm intro",
                "ai_generated_at": "2023-07-01T11:52:50Z"
            ]
        )
    }
}
