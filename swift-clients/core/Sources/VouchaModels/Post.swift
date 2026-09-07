import Foundation

public enum PostType: String, Codable, Hashable, Sendable {
    case discussion, review, dataPoint = "data_point", comment
    case article, blogPost = "blog_post", story, link
    case topicRecommendation = "topic_recommendation"
}

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

public struct Post: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let slug: String?
    public let postType: PostType
    public let title: String?
    public let declaredLanguage: String?
    public let linguaRsDetectedLanguage: String?
    public let markdown: String?
    public let html: String?
    @TolerantNullable
    public var parentId: String?
    @RequiredNullable
    public var rootId: String?
    /// Nullable: `maskAnonymousPost` sets this to `null` for other users' anonymous posts.
    public let createdById: String?
    public let createdAt: Date
    public let broadcast: BroadcastScope?
    public let privacy: PostPrivacy
    public let isAnonymous: Bool
    @RequiredNullable
    public var communityId: String?
    public let clearanceStatus: ClearanceStatus?
    @TolerantNullable
    public var approvedAt: Date?
    @TolerantNullable
    public var inReviewAt: Date?
    @TolerantNullable
    public var rejectedAt: Date?
    public let metrics: PostMetrics?
    public let election: PostElection?
    public let createdBy: PublicUser?
    public let updatedAt: Date?
    @RequiredNullable
    public var deletedAt: Date?
    @RequiredNullable
    public var deletedById: String?
    @RequiredNullable
    public var lockedAt: Date?
    @RequiredNullable
    public var lockedById: String?
    public let canEditContent: Bool?
    public let canDelete: Bool?
    public let canLock: Bool?
    public let aiSummaryMarkdown: String?
    @RequiredNullable
    public var archivedAt: Date?
    @RequiredNullable
    public var archivedById: String?
    @RequiredNullable
    public var clearanceReason: String?
    @RequiredNullable
    public var clearanceUpdatedAt: Date?
    @RequiredNullable
    public var spamDetectionCreatedAt: Date?
    @RequiredNullable
    public var spamDetectionFlagged: Bool?
    @RequiredNullable
    public var spamDetectionResults: DecodedJSONValue?
    @RequiredNullable
    public var spamDetectionScore: Double?
    @RequiredNullable
    public var updatedById: String?
    public let postExplicitCategories: [PostExplicitCategory]?
    public let postHashtags: [PostHashtag]?

    /** compatibility initializer is in Post+Compatibility.swift */
    private enum TransportCodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, slug, postType, title, declaredLanguage, linguaRsDetectedLanguage, markdown, html, parentId, rootId
        case createdById, createdAt, broadcast, privacy, isAnonymous, communityId
        case clearanceStatus, approvedAt, inReviewAt, rejectedAt
        case metrics, election, createdBy, updatedAt, deletedAt, deletedById
        case lockedAt, lockedById, canEditContent, canDelete, canLock
        case aiSummaryMarkdown, archivedAt, archivedById, clearanceReason
        case clearanceUpdatedAt, spamDetectionCreatedAt, spamDetectionFlagged
        case spamDetectionResults, spamDetectionScore, updatedById
        case postExplicitCategories, postHashtags
    }
}

extension Post {
    public init(
        id: String,
        slug: String?,
        postType: PostType,
        title: String?,
        declaredLanguage: String? = nil,
        linguaRsDetectedLanguage: String? = nil,
        markdown: String?,
        html: String?,
        parentId: String?,
        rootId: String?,
        createdById: String?,
        createdAt: Date,
        broadcast: BroadcastScope?,
        privacy: PostPrivacy,
        isAnonymous: Bool,
        communityId: String?,
        clearanceStatus: ClearanceStatus?,
        metrics: PostMetrics? = nil,
        election: PostElection? = nil,
        createdBy: PublicUser? = nil,
        updatedAt: Date? = nil,
        deletedAt: Date? = nil,
        deletedById: String? = nil,
        lockedAt: Date? = nil,
        lockedById: String? = nil,
        canEditContent: Bool? = nil,
        canDelete: Bool? = nil,
        canLock: Bool? = nil,
        postExplicitCategories: [PostExplicitCategory]? = nil,
        postHashtags: [PostHashtag]? = nil
    ) {
        entityType = nil
        self.id = id
        self.slug = slug
        self.postType = postType
        self.title = title
        self.declaredLanguage = declaredLanguage
        self.linguaRsDetectedLanguage = linguaRsDetectedLanguage
        self.markdown = markdown
        self.html = html
        self.parentId = parentId
        self.rootId = rootId
        self.createdById = createdById
        self.createdAt = createdAt
        self.broadcast = broadcast
        self.privacy = privacy
        self.isAnonymous = isAnonymous
        self.communityId = communityId
        self.clearanceStatus = clearanceStatus
        approvedAt = nil
        inReviewAt = nil
        rejectedAt = nil
        self.metrics = metrics
        self.election = election
        self.createdBy = createdBy
        self.updatedAt = updatedAt
        self.deletedAt = deletedAt
        self.deletedById = deletedById
        self.lockedAt = lockedAt
        self.lockedById = lockedById
        self.canEditContent = canEditContent
        self.canDelete = canDelete
        self.canLock = canLock
        aiSummaryMarkdown = nil
        archivedAt = nil
        archivedById = nil
        clearanceReason = nil
        clearanceUpdatedAt = nil
        spamDetectionCreatedAt = nil
        spamDetectionFlagged = nil
        spamDetectionResults = nil
        spamDetectionScore = nil
        updatedById = nil
        self.postExplicitCategories = postExplicitCategories
        self.postHashtags = postHashtags
    }

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, slug, postType, title, declaredLanguage, linguaRsDetectedLanguage, markdown, html, parentId, rootId
        case createdById, createdAt, broadcast, privacy, isAnonymous, communityId
        case clearanceStatus, approvedAt, inReviewAt, rejectedAt
        case metrics, election, createdBy, updatedAt, deletedAt, deletedById
        case lockedAt, lockedById, canEditContent, canDelete, canLock
        case aiSummaryMarkdown, archivedAt, archivedById, clearanceReason
        case clearanceUpdatedAt, spamDetectionCreatedAt, spamDetectionFlagged
        case spamDetectionResults, spamDetectionScore, updatedById
        case postExplicitCategories, postHashtags
    }
}
