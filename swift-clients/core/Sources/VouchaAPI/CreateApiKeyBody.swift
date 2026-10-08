import Foundation
import VouchaModels

public enum ApiKeyLifetimeChoice: Sendable {
    case serverDefault
    case days(Int)
    case unlimited
}

struct CreateApiKeyBody: Encodable {
    let label: String
    let type: ApiKeyType
    let permissions: [String]
    let lifetime: ApiKeyLifetimeChoice

    private enum CodingKeys: String, CodingKey {
        case label, type, permissions
        case lifetimeDays = "lifetime_days"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(label, forKey: .label)
        try container.encode(type, forKey: .type)
        try container.encode(permissions, forKey: .permissions)
        switch lifetime {
        case .serverDefault: break
        case let .days(days): try container.encode(days, forKey: .lifetimeDays)
        case .unlimited: try container.encodeNil(forKey: .lifetimeDays)
        }
    }
}
