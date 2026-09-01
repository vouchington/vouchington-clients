import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaModels

extension NativePostComposeViewModel {
    func attestedCreateEndpoint(idempotencyKey: String) async -> Endpoint? {
        guard let appAttestationService, appAttestationService.isSupported else { return nil }
        let actionTag: AppAttestActionTag = communityIdOrSlug == nil ? .postsCreate : .communitiesCreatePost
        let endpoint = makeCreateEndpoint(turnstileToken: nil, idempotencyKey: idempotencyKey)
        guard let headers = try? await appAttestationService.assertionHeaders(actionTag: actionTag) else {
            return nil
        }
        return endpoint.withHeaders(headers)
    }

    func submit(client: APIClient, endpoint: Endpoint, fallbackToTurnstile: Bool, canonicalIntent: String) async {
        do {
            let response: PostEnvelope? = try await emailVerificationGate.perform(rollbackOnFailure: {
                drafts.insert(draftRow, at: 0)
                turnstileToken = nil
            }, {
                do {
                    return try await client.send(endpoint)
                } catch let error as VouchaError
                    where fallbackToTurnstile && error.isRecoverableByTurnstileFallback {
                    if error.isAttestationKeyRejected {
                        appAttestationService?.forgetCachedKey()
                    }
                    if let turnstileToken {
                        guard let idempotencyKey = endpoint.headers["Idempotency-Key"] else { return nil }
                        return try await client.send(makeCreateEndpoint(
                            turnstileToken: turnstileToken, idempotencyKey: idempotencyKey
                        ))
                    } else {
                        drafts.insert(draftRow, at: 0)
                        state = .required(.turnstile)
                    }
                    return nil
                }
            })
            guard let response else { return }
            await contributionIdentity.complete(surface: "post", canonicalIntent: canonicalIntent)
            publishedPostId = response.post.id
            resetFormAfterPublish()
            state = .loaded
        } catch let error as ContributionAdmissionFailure {
            state = .error(.api(statusCode: error.statusCode, preconditionCode: error.code))
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            logger.error("Publish failed: \(error.localizedDescription)")
        }
    }
}
