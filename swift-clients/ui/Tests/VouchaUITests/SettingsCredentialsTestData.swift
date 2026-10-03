import Foundation
import VouchaModels

enum SettingsCredentialsTestData {
    static let catalog = Data(
        #"{"scopes":[{"scope":"feed:read","resource":"feed","action":"read","audience":"api","requires":null,"description_key":null,"surfaces":["api-key"]},{"scope":"data:read","resource":"data","action":"read","audience":"user","requires":null,"description_key":null,"surfaces":["api-key","oauth"]},{"scope":"data:write","resource":"data","action":"write","audience":"user","requires":"data:read","description_key":null,"surfaces":["api-key","oauth"]}]}"#
            .utf8
    )

    static func grants(_ ids: [String], hasMore: Bool = false, lastUsed: Bool = false) -> Data {
        let rows = ids.map { id in
            """
            {"id":"\(id)","client":{"id":"client-\(id)","client_id":"public-\(id)",
            "client_name":"Agent \(id)","verified":true},"resource":"https://voucha.ai/api/v1/mcp",
            "scopes":["data:read"],"consented_at":"2026-09-01T12:00:00Z",
            "last_used_at":\(lastUsed ? "\"2026-09-02T12:00:00Z\"" : "null")}
            """
        }.joined(separator: ",")
        return Data("""
        {"results":[\(rows)],"page_info":{"has_next_page":\(hasMore),
        "end_cursor":\(hasMore ? "\"grant-next+/=\"" : "null"),"start_cursor":null}}
        """.utf8)
    }
}
