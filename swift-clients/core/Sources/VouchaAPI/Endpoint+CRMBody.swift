import Foundation
import VouchaModels

public struct CrmContactSocialAccountInput: Encodable {
    public let platform: CrmSocialPlatform
    public let handle: String
    public let profileUrl: String?
    public let followerCount: Int?

    public init(
        platform: CrmSocialPlatform,
        handle: String,
        profileUrl: String? = nil,
        followerCount: Int? = nil
    ) {
        self.platform = platform
        self.handle = handle
        self.profileUrl = profileUrl
        self.followerCount = followerCount
    }
}

struct CrmContactCreateBody: Encodable {
    let name: String
    let email: String
    let phone: String?
    let vertical: CrmContactVertical?
    let contactType: CrmContactType?
    let source: CrmContactSource?
    let followerCount: Int?
    let notes: String?
    let metadata: [String: DecodedJSONValue]?
    let assignedToId: String?
    let socialAccounts: [CrmContactSocialAccountInput]?
}

struct CrmContactUpdateBody: Encodable {
    let name: String?
    let email: String?
    let phone: String?
    let vertical: CrmContactVertical?
    let contactType: CrmContactType?
    let followerCount: Int?
    let notes: String?
    let metadata: [String: DecodedJSONValue]?
    let assignedToId: String?
    let contactedAt: Date?
    let respondedAt: Date?
    let convertedAt: Date?
    let optedOutAt: Date?
}

struct CrmEmailSendBody: Encodable {
    let subject: String
    let bodyHtml: String?
    let bodyText: String?
    let emailProvider: CrmEmailProvider
    let ctaUrl: String?
    let aiPrompt: String?
    let aiGeneratedAt: Date?

    enum CodingKeys: String, CodingKey {
        case subject
        case bodyHtml
        case bodyText
        case emailProvider
        case ctaUrl
        case aiPrompt
        case aiGeneratedAt
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(subject, forKey: .subject)
        try container.encodeIfPresent(bodyHtml, forKey: .bodyHtml)
        try container.encodeIfPresent(bodyText, forKey: .bodyText)
        try container.encode(emailProvider, forKey: .emailProvider)
        try container.encodeIfPresent(ctaUrl, forKey: .ctaUrl)
        try container.encodeIfPresent(aiPrompt, forKey: .aiPrompt)
        if let aiGeneratedAt {
            try container.encode(
                ISO8601DateFormatter.vouchaRequest.string(from: aiGeneratedAt),
                forKey: .aiGeneratedAt
            )
        }
    }
}

struct CrmNoteBody: Encodable {
    let body: String
}

struct CrmContactEmailDraftBody: Encodable {
    let prompt: String?
    let tone: String?
}

struct CrmImportBody: Encodable {
    let csv: String
}

private extension ISO8601DateFormatter {
    static let vouchaRequest: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter
    }()
}
