import VouchaAPI
import VouchaModels

@MainActor
enum NativeChatMCPToolCatalog {
    static func listAll(
        authorized: MemberMCPAuthorizedClient,
        assertCurrent: () throws -> Void
    ) async throws -> DecodedJSONValue {
        var cursor: String?
        var seenCursors = Set<String>()
        var allTools: [DecodedJSONValue] = []
        var firstPage: [String: DecodedJSONValue]?
        repeat {
            let page = try await authorized.listTools(cursor: cursor)
            try assertCurrent()
            guard case let .object(result) = page,
                  case let .array(tools)? = result["tools"],
                  allTools.count + tools.count <= 1_024
            else { throw NativeChatMCPNetworkService.Failure.invalidToolCatalog }
            if firstPage == nil { firstPage = result }
            allTools.append(contentsOf: tools)
            switch result["nextCursor"] {
            case .none, .some(.null):
                cursor = nil
            case let .some(.string(next)):
                guard seenCursors.insert(next).inserted, seenCursors.count <= 100 else {
                    throw NativeChatMCPNetworkService.Failure.invalidToolCatalog
                }
                cursor = next
            default:
                throw NativeChatMCPNetworkService.Failure.invalidToolCatalog
            }
        } while cursor != nil
        var result = firstPage ?? [:]
        result["tools"] = .array(allTools)
        result.removeValue(forKey: "nextCursor")
        return .object(result)
    }
}
