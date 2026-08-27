import Foundation

extension NativeTopicRecommendationViewModel {
    var topicTypeRequirementsMet: Bool {
        switch topicType {
        case "referral_program":
            !exampleReferralLink.trimmed.isEmpty
        case "card":
            !landingPageUrls.nativeLines.isEmpty
        default:
            true
        }
    }

    func apply(_ response: NativeTopicRecommendationPostEnvelope) {
        let post = response.post
        title = post.title ?? ""
        bodyText = post.markdown ?? ""
        let fields = post.topicRecommendation
        topicTitle = fields?.topicTitle ?? ""
        topicSlug = fields?.topicSlug ?? ""
        topicMarkdown = fields?.topicMarkdown ?? ""
        topicHostname = fields?.hostname?.hostname ?? ""
        topicHostnames = fields?.hostnames?.map(\.hostname).joined(separator: "\n") ?? ""
        aliases = fields?.aliases?.joined(separator: "\n") ?? ""
        topicType = fields?.topicType ?? "topic"
        exampleReferralLink = fields?.exampleReferralLink ?? ""
        landingPageUrls = fields?.landingPageUrls?.joined(separator: "\n") ?? ""
        postElection = response.postElection.map { election in
            .init(
                votesScoreNet: election.votesScoreNet,
                votesCountUp: election.votesCountUp,
                votesCountDown: election.votesCountDown,
                myVote: response.electionVote?.choice ?? election.myVote
            )
        }
        currentVote = response.electionVote?.choice ?? response.postElection?.myVote
    }

    func resetAfterCreate() {
        title = ""
        bodyText = ""
        topicTitle = ""
        topicSlug = ""
        topicMarkdown = ""
        topicHostname = ""
        topicHostnames = ""
        aliases = ""
        topicType = "topic"
        exampleReferralLink = ""
        landingPageUrls = ""
        turnstileToken = nil
        postElection = nil
        currentVote = nil
    }

}
