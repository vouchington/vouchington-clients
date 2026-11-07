import Foundation

public enum DataPointVertical: String, Codable, Hashable, Sendable {
    case creditCard = "credit_card"
    case bankAccount = "bank_account"
}

public enum BroadcastScope: String, Codable, Sendable {
    case everyone, users, followers, mutualFollowers = "mutual_followers"
}

public enum PostPrivacy: String, Codable, Sendable {
    case `public`, `private`
}

public enum ClearanceStatus: String, Codable, Sendable {
    case pending, approved, rejected, inReview = "in_review"
}
