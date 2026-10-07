/// The server's current account classification. A nil value identifies an ordinary user.
public enum AccountType: String, Codable, Sendable {
    case official
    case system
    case aiAgent = "ai_agent"
}
