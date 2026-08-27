import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class EndpointCRMSocialAccountTests: XCTestCase {
    func testCreateCrmContactEncodesSocialAccountInputs() {
        assertEndpoint(
            Endpoint.createCrmContact(
                name: "Alice Creator",
                email: "alice@example.test",
                phone: "+1-415-555-0100",
                vertical: .creditCards,
                contactType: .influencer,
                source: .manual,
                followerCount: 250_000,
                notes: "Creator outreach contact",
                metadata: ["tags": .array([.string("creator"), .string("travel")]), "vip": .bool(true)],
                assignedToId: "user-1",
                socialAccounts: [
                    CrmContactSocialAccountInput(
                        platform: .instagram,
                        handle: "@alicecreator",
                        profileUrl: "https://instagram.com/alicecreator",
                        followerCount: 250_000
                    )
                ]
            ),
            method: .POST,
            path: "/api/v1/crm/contacts",
            body: [
                "name": "Alice Creator",
                "email": "alice@example.test",
                "phone": "+1-415-555-0100",
                "vertical": "credit_cards",
                "contact_type": "influencer",
                "source": "manual",
                "follower_count": 250_000,
                "notes": "Creator outreach contact",
                "metadata": ["tags": ["creator", "travel"], "vip": true],
                "assigned_to_id": "user-1",
                "social_accounts": [
                    [
                        "platform": "instagram",
                        "handle": "@alicecreator",
                        "profile_url": "https://instagram.com/alicecreator",
                        "follower_count": 250_000
                    ]
                ]
            ]
        )
    }
}
