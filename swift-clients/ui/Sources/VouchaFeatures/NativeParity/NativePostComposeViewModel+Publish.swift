import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaModels

extension NativePostComposeViewModel {
    func attestedCreateEndpoint() async -> Endpoint? {
        guard let appAttestationService, appAttestationService.isSupported else { return nil }
        let actionTag: AppAttestActionTag = communityIdOrSlug == nil ? .postsCreate : .communitiesCreatePost
        let endpoint = makeCreateEndpoint(turnstileToken: nil)
        guard let headers = try? await appAttestationService.assertionHeaders(actionTag: actionTag) else {
            return nil
        }
        return endpoint.withHeaders(headers)
    }

    func submit(client: APIClient, endpoint: Endpoint, fallbackToTurnstile: Bool) async {
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
                        return try await client.send(
                            makeCreateEndpoint(turnstileToken: turnstileToken)
                        )
                    } else {
                        drafts.insert(draftRow, at: 0)
                        state = .required(.turnstile)
                    }
                    return nil
                }
            })
            guard let response else { return }
            publishedPostId = response.post.id
            resetFormAfterPublish()
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            logger.error("Publish failed: \(error.localizedDescription)")
        }
    }
}
