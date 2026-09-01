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
        let canonicalIntent = body.canonicalIntent
        let idempotencyKey = await contributionIdentity.key(
            surface: "topic-recommendation", canonicalIntent: canonicalIntent
        )
        if !isEdit, let client, let attested = await attestedEndpoint(idempotencyKey: idempotencyKey) {
            await submit(client: client, endpoint: attested, fallbackToTurnstile: true, canonicalIntent: canonicalIntent)
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
        await submit(
            client: client,
            endpoint: endpoint(idempotencyKey: idempotencyKey),
            fallbackToTurnstile: false,
            canonicalIntent: canonicalIntent
        )
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

    private func attestedEndpoint(idempotencyKey: String) async -> Endpoint? {
        guard let appAttestationService, appAttestationService.isSupported else { return nil }
        guard let headers = try? await appAttestationService.assertionHeaders(
            actionTag: .topicRecommendationsCreate
        ) else {
            return nil
        }
        return endpoint(idempotencyKey: idempotencyKey).withHeaders(headers)
    }

    private func submit(
        client: APIClient,
        endpoint: Endpoint,
        fallbackToTurnstile: Bool,
        canonicalIntent: String
    ) async {
        do {
            let response: NativeTopicRecommendationPostEnvelope = try await client.send(endpoint)
            savedPostId = response.post.id
            await contributionIdentity.complete(surface: "topic-recommendation", canonicalIntent: canonicalIntent)
            if !isEdit {
                resetAfterCreate()
            }
            state = .loaded
        } catch let error as VouchaError where fallbackToTurnstile && error.isRecoverableByTurnstileFallback {
            if error.isAttestationKeyRejected {
                appAttestationService?.forgetCachedKey()
            }
            if turnstileToken != nil {
                await submit(
                    client: client,
                    endpoint: self.endpoint(idempotencyKey: endpoint.headers["Idempotency-Key"]),
                    fallbackToTurnstile: false,
                    canonicalIntent: canonicalIntent
                )
            } else {
                state = .required
            }
        } catch let error as ContributionAdmissionFailure {
            state = .error(.api(statusCode: error.statusCode, preconditionCode: error.code))
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            logger.error("Save recommendation failed: \(error.localizedDescription)")
        }
    }

    private func endpoint(idempotencyKey: String? = nil) -> Endpoint {
        if let recommendationId {
            Endpoint.updateTopicRecommendation(id: recommendationId, body: body)
        } else {
            Endpoint(
                .POST,
                path: "/api/v1/topic-recommendations",
                headers: idempotencyKey.map { ["Idempotency-Key": $0] } ?? [:],
                body: body
            )
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

private extension NativeTopicRecommendationMutationBody {
    var canonicalIntent: String {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.outputFormatting = [.sortedKeys]
        return (try? encoder.encode(self)).map { $0.base64EncodedString() } ?? ""
    }
}
