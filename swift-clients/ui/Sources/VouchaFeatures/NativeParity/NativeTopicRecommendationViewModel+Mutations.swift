import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeTopicRecommendationViewModel {
    func load() async {
        guard let recommendationId, let client, case .idle = state else { return }
        state = .loading
        do {
            let response: NativeTopicRecommendationPostEnvelope = try await client.send(.topicRecommendation(
                id: recommendationId
            ))
            apply(response)
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            logger.error("Load recommendation failed: \(error.localizedDescription)")
        }
    }

    func submit() async {
        guard canSubmit, !isLoading else { return }
        state = .loading
        if !isEdit, let client, let attested = await attestedEndpoint() {
            await submit(client: client, endpoint: attested, fallbackToTurnstile: true)
            return
        }
        if !isEdit, turnstileToken == nil {
            state = .required
            return
        }
        guard let client else {
            savedPostId = "draft"
            state = .loaded
            return
        }
        await submit(client: client, endpoint: endpoint, fallbackToTurnstile: false)
    }

    func vote(_ choice: ElectionVoteChoice?) async {
        guard let client, let postId = savedPostId ?? recommendationId, !isLoading, !isVoting else { return }
        isVoting = true
        defer { isVoting = false }
        let previousElection = postElection
        let previousVote = currentVote
        if let election = postElection {
            let counts = ElectionVoteCountReconciler.reconcile(
                previous: currentVote ?? election.myVote,
                next: choice,
                positive: election.votesCountUp,
                negative: election.votesCountDown
            )
            postElection = .init(
                votesScoreNet: election.votesScoreNet,
                votesCountUp: counts.positive,
                votesCountDown: counts.negative,
                myVote: choice
            )
        }
        currentVote = choice
        let _: EmptyResponse? = try? await emailVerificationGate.perform(rollbackOnFailure: {
            postElection = previousElection
            currentVote = previousVote
        }, {
            if let choice {
                try await client.send(.votePost(postId: postId, choice: choice))
            } else {
                try await client.send(.clearPostVote(postId: postId))
            }
        })
    }

    private func attestedEndpoint() async -> Endpoint? {
        guard let appAttestationService, appAttestationService.isSupported else { return nil }
        guard let headers = try? await appAttestationService.assertionHeaders(
            actionTag: .topicRecommendationsCreate
        ) else {
            return nil
        }
        return endpoint.withHeaders(headers)
    }

    private func submit(client: APIClient, endpoint: Endpoint, fallbackToTurnstile: Bool) async {
        do {
            let response: NativeTopicRecommendationPostEnvelope = try await client.send(endpoint)
            savedPostId = response.post.id
            if !isEdit {
                resetAfterCreate()
            }
            state = .loaded
        } catch let error as VouchaError where fallbackToTurnstile && error.isRecoverableByTurnstileFallback {
            if error.isAttestationKeyRejected {
                appAttestationService?.forgetCachedKey()
            }
            if turnstileToken != nil {
                await submit(client: client, endpoint: self.endpoint, fallbackToTurnstile: false)
            } else {
                state = .required
            }
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            logger.error("Save recommendation failed: \(error.localizedDescription)")
        }
    }

    private var endpoint: Endpoint {
        if let recommendationId {
            Endpoint.updateTopicRecommendation(id: recommendationId, body: body)
        } else {
            Endpoint(.POST, path: "/api/v1/topic-recommendations", body: body)
        }
    }

    var body: NativeTopicRecommendationMutationBody {
        .init(
            title: title.trimmedOrNil,
            markdown: bodyText.trimmed,
            topicTitle: topicTitle.trimmed,
            topicSlug: topicSlug.trimmed,
            topicMarkdown: topicMarkdown.trimmedOrNil,
            topicHostname: topicHostname.trimmedOrNil,
            topicHostnames: topicHostnames.nativeLines,
            topicAliases: aliases.nativeLines,
            topicType: topicType,
            exampleReferralLink: exampleReferralLink.trimmedOrNil,
            landingPageUrls: landingPageUrls.nativeLines,
            cfTurnstileResponse: isEdit ? nil : turnstileToken
        )
    }
}
