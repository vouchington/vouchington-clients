import Foundation

struct NativeAgentPath {
    let value: String

    static func detail(_ agentId: String) -> Self {
        .init(value: "/agent/\(segment(agentId))")
    }

    static func conversation(agentIdOrSlug: String, conversationId: String) -> Self {
        .init(value: "\(detail(agentIdOrSlug).value)/conversation/\(segment(conversationId))")
    }

    private static let allowed: CharacterSet = {
        var characters = CharacterSet.urlPathAllowed
        characters.remove(charactersIn: "/:%")
        return characters
    }()

    private static func segment(_ rawValue: String) -> String {
        rawValue.addingPercentEncoding(withAllowedCharacters: allowed) ?? rawValue
    }
}
