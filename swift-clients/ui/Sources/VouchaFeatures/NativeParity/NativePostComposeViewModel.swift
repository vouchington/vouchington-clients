import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization
import VouchaModels

public enum NativePostComposeRequirement: Sendable {
    case turnstile
}

public enum NativePostComposeState: Sendable {
    case idle
    case loading
    case loaded
    case required(NativePostComposeRequirement)
    case error(VouchaError)
}

public struct NativePostComposeReviewTopicRatingDraft: Identifiable, Sendable {
    public let id = UUID()
    public var topicId = ""
    public var rating = 0

    public init(topicId: String = "", rating: Int = 0) {
        self.topicId = topicId
        self.rating = rating
    }
}

@Observable
@MainActor
public final class NativePostComposeViewModel {
    public var title = ""
    public var bodyText = ""
    public var linkURL = ""
    public var postType: PostType = .discussion
    public var reviewTopicRatings: [NativePostComposeReviewTopicRatingDraft] = [
        .init()
    ]
    public var categoryDrafts: [NativePostComposeCategoryDraft] = []
    public var dataPointVertical: DataPointVertical?
    public var dataPointStructuredDataJSON = ""
    public var turnstileToken: String?
    var images: [NativePostComposeImageDraft] = []
    var isUploadingImages = false
    var imageUploadErrorMessage: UiVerbatimText?
    private var activeImageUploadBatches = 0
    public internal(set) var drafts: [NativeRouteDestinationRow] = []
    public internal(set) var publishedPostId: String?
    public internal(set) var state: NativePostComposeState = .idle
    let emailVerificationGate = EmailVerificationGatedMutation()

    let client: APIClient?
    let imageUploadService: ImageUploadService?
    let communityIdOrSlug: String?
    let isAdministrator: Bool
    let appAttestationService: AppAttestationService?
    let logger = VouchaLogger(category: "NativePostComposeViewModel")

    public init(
        client: APIClient?,
        communityIdOrSlug: String? = nil,
        initialPostType: PostType = .discussion,
        isAdministrator: Bool = false,
        imageUploadProtocolClasses: [AnyClass]? = nil,
        appAttestationService: AppAttestationService? = nil
    ) {
        self.client = client
        imageUploadService = client.map {
            ImageUploadService(client: $0, uploadProtocolClasses: imageUploadProtocolClasses)
        }
        self.communityIdOrSlug = communityIdOrSlug
        self.isAdministrator = isAdministrator
        postType = Self.normalizedPostType(
            initialPostType,
            communityIdOrSlug: communityIdOrSlug,
            isAdministrator: isAdministrator
        )
        self.appAttestationService = appAttestationService ?? AppAttestationService.makeDefault(client: client)
    }

    public var canSubmit: Bool {
        if isUploadingImages {
            return false
        }
        guard availablePostTypes.contains(postType) else { return false }
        guard postType != .discussion || hasValidDiscussionCategoryDrafts else { return false }
        return switch postType {
        case .link:
            !linkURL.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        case .review:
            hasCoreText && hasValidReviewRatings
        case .dataPoint:
            hasCoreText && dataPointVertical != nil && parseStructuredData() != nil
        case .story:
            false
        default:
            hasCoreText
        }
    }

    public var canSaveDraft: Bool {
        hasDraftContent && !isUploadingImages
    }

    public var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    /// Whether `publish()` can proceed without first requiring a manual Turnstile challenge —
    /// true once a Turnstile token has been captured, or whenever App Attest is available as a
    /// hardware-backed substitute for it.
    public var canPublish: Bool {
        canSubmit && !isLoading && (turnstileToken != nil || appAttestationService?.isSupported == true)
    }

    public func addReviewTopicRating() {
        reviewTopicRatings.append(.init())
    }

    public func removeReviewTopicRating(at index: Int) {
        guard reviewTopicRatings.indices.contains(index) else { return }
        reviewTopicRatings.remove(at: index)
        if reviewTopicRatings.isEmpty {
            reviewTopicRatings = [.init()]
        }
    }

    public func saveDraft() {
        guard hasDraftContent else { return }
        guard !isUploadingImages else {
            imageUploadErrorMessage = .app(UiMessage(.nativeSwiftPostComposeImageWaitForDraft))
            return
        }
        drafts.insert(draftRow, at: 0)
        resetFormAfterSave()
        state = .loaded
    }

    func beginImageUploadBatch() {
        activeImageUploadBatches += 1
        isUploadingImages = true
    }

    func endImageUploadBatch() {
        activeImageUploadBatches = max(0, activeImageUploadBatches - 1)
        isUploadingImages = activeImageUploadBatches > 0
    }

    public func publish() async {
        guard !isLoading else { return }
        guard hasDraftContent, canSubmit else { return }
        state = .loading
        guard let client else {
            saveDraft()
            return
        }

        if let endpoint = await attestedCreateEndpoint() {
            await submit(client: client, endpoint: endpoint, fallbackToTurnstile: true)
            return
        }

        guard let turnstileToken else {
            drafts.insert(draftRow, at: 0)
            state = .required(.turnstile)
            return
        }

        await submit(
            client: client,
            endpoint: makeCreateEndpoint(turnstileToken: turnstileToken),
            fallbackToTurnstile: false
        )
    }

}

extension String {
    var trimmedOrNil: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
