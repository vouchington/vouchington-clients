import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SettingsPaginationRaceTests: XCTestCase {
    func testStalePageCannotRestoreARevokedApiKey() throws {
        let viewModel = SettingsViewModel(client: nil)
        let stalePage = try decodeApiKeyPage()
        _ = viewModel.beginSettingsLoad()

        viewModel.reconcileRevokedApiKey(id: "key-1")
        viewModel.replaceApiKeyPage(stalePage)

        XCTAssertTrue(viewModel.apiKeys.isEmpty)
    }

    private func decodeApiKeyPage() throws -> SettingsListResponse<ApiKey> {
        let data = Data(
            #"""
            {"results":[{"id":"key-1","user_id":"user-1","prefix":"voucha","type":"rss",
            "label":"Reader","permissions":[],"created_at":"2026-07-11T12:00:00Z","last_used_at":null,
            "revoked_at":null,"expires_at":null,"expiry_reminder_sent_at":null,"replaced_by_api_key_id":null,"updated_at":"2026-07-11T12:00:00Z"}],"page_info":{"has_next_page":false,
            "start_cursor":null,"end_cursor":null}}
            """#.utf8
        )
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(SettingsListResponse<ApiKey>.self, from: data)
    }
}
