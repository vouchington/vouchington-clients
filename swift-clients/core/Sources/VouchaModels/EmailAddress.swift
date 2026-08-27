import Foundation

public struct EmailAddress: Codable, Identifiable, Sendable {
    public var id: String {
        emailAddress
    }

    public let emailAddress: String
    public let isPrimary: Bool
    public let createdAt: Date
}

public struct EmailAddressListResponse: Codable, Sendable {
    public let results: [EmailAddress]
    public let pageInfo: Page<EmailAddress>.PageInfo
}

public struct EmailAddressVerificationRequestResponse: Codable, Sendable {
    public let emailAddress: String
}
