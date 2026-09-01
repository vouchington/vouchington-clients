import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaModels

enum NativeTopicRecommendationState {
    case idle
    case loading
    case loaded
    case required
    case error(VouchaError)
}

@Observable
@MainActor
final class NativeTopicRecommendationViewModel {
    var title = ""
    var bodyText = ""
    var topicTitle = ""
    var topicSlug = ""
    var topicMarkdown = ""
    var topicHostname = ""
    var topicHostnames = ""
    var aliases = ""
    var topicType = "topic"
    var exampleReferralLink = ""
    var landingPageUrls = ""
    var turnstileToken: String?
    var savedPostId: String?
    var state: NativeTopicRecommendationState = .idle
    var postElection: PostElection?
    var currentVote: ElectionVoteChoice?
    var isVoting = false

    let recommendationId: String?
    let client: APIClient?
    let appAttestationService: AppAttestationService?
    let contributionIdentity = ContributionRequestIdentity()
    let logger = VouchaLogger(category: "NativeTopicRecommendationViewModel")

    init(client: APIClient?, recommendationId: String? = nil, appAttestationService: AppAttestationService? = nil) {
        self.client = client
        self.recommendationId = recommendationId
        self.appAttestationService = appAttestationService ?? AppAttestationService.makeDefault(client: client)
    }

    var isEdit: Bool {
        recommendationId != nil
    }

    var canSubmit: Bool {
        !topicTitle.trimmed.isEmpty &&
            !topicSlug.trimmed.isEmpty &&
            !bodyText.trimmed.isEmpty &&
            topicTypeRequirementsMet
    }

    var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    let emailVerificationGate = EmailVerificationGatedMutation()
}
