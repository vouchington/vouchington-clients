import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

extension MemberMCPClient {
    func sessionForReply(to method: String, response: HTTPURLResponse, previous: String?) throws -> String? {
        guard method == "initialize", let session = response.value(forHTTPHeaderField: "Mcp-Session-Id") else {
            return previous
        }
        guard !session.isEmpty, session.utf8.allSatisfy({ 0x21 ... 0x7E ~= $0 })
        else { throw Failure.invalidResponse }
        sessionId = session
        return session
    }
}
