public struct AuthSessionMCPConnection: Codable, Sendable {
    public let grantId: String
    public let clientName: String
    public let attestationStatus: String
    public let lastMintedAt: String
}
