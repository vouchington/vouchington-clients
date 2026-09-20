import Foundation
import VouchaAPI
import VouchaModels

extension NativeReviewQueueViewModel {
    private static let minimumCooldownRefetchDelayNanoseconds: UInt64 = 5_000_000_000

    func canRevealMedia(postId: String) -> Bool {
        guard !revealedPostIds.contains(postId),
              let item = items.first(where: { $0.id == postId }),
              item.post.mediaReveal.requiresReveal,
              !item.post.mediaReveal.images.isEmpty,
              exposureState != nil,
              !exposureIsStale,
              inFlightRevealPostId == nil
        else { return false }
        return exposureState?.inCooldown == false
    }

    func isMediaRevealed(postId: String) -> Bool {
        revealedPostIds.contains(postId)
    }

    func revealMedia(postId: String) async {
        guard canRevealMedia(postId: postId), let client else { return }
        let observedRequestRevision = exposureRequestRevision
        let observedRevealContextRevision = revealContextRevision
        inFlightRevealPostId = postId
        defer { inFlightRevealPostId = nil }
        do {
            let response: ModerationExposureResponse = try await client.send(
                .recordModerationReveal(postId: postId, surface: .reviewQueue)
            )
            guard !Task.isCancelled,
                  isAuthorized,
                  observedRequestRevision == exposureRequestRevision,
                  observedRevealContextRevision == revealContextRevision,
                  items.contains(where: { $0.id == postId }),
                  inFlightRevealPostId == postId
            else {
                ignoreObsoleteReveal(observedRequestRevision: observedRequestRevision)
                return
            }
            revealedPostIds.insert(postId)
            exposureOutcomeRevision += 1
            acceptExposure(response.exposure)
        } catch {
            ignoreObsoleteReveal(observedRequestRevision: observedRequestRevision)
        }
    }

    func refetchExposure() async {
        guard isAuthorized, let client else { return }
        exposureRequestRevision += 1
        let requestRevision = exposureRequestRevision
        let observedOutcomeRevision = exposureOutcomeRevision
        do {
            let response: ModerationExposureResponse = try await client.send(.moderationExposure())
            guard !Task.isCancelled,
                  requestRevision == exposureRequestRevision,
                  observedOutcomeRevision == exposureOutcomeRevision
            else { return }
            acceptExposure(response.exposure)
        } catch {
            guard !Task.isCancelled,
                  requestRevision == exposureRequestRevision,
                  observedOutcomeRevision == exposureOutcomeRevision
            else { return }
            exposureIsStale = true
        }
    }

    func cooldownExpiryReached() async {
        cancelScheduledCooldownRefetch()
        await refetchExposure()
    }

    private func acceptExposure(_ exposure: ModerationExposureState) {
        exposureState = exposure
        exposureIsStale = false
        scheduleCooldownRefetch(for: exposure)
    }

    private func ignoreObsoleteReveal(observedRequestRevision: Int) {
        guard observedRequestRevision == exposureRequestRevision else { return }
        exposureOutcomeRevision += 1
        markExposureStale()
    }

    private func markExposureStale() {
        exposureIsStale = true
        cancelScheduledCooldownRefetch()
    }

    private func scheduleCooldownRefetch(for exposure: ModerationExposureState) {
        cancelScheduledCooldownRefetch()
        guard exposure.inCooldown, let cooldownEndsAt = exposure.cooldownEndsAt else { return }
        let scheduleRevision = cooldownRefetchRevision
        let serverDelayNanoseconds = UInt64(max(0, cooldownEndsAt.timeIntervalSinceNow) * 1_000_000_000)
        let delayNanoseconds = max(Self.minimumCooldownRefetchDelayNanoseconds, serverDelayNanoseconds)
        let cooldownSleep = cooldownSleep
        cooldownRefetchTask = Task { [weak self] in
            do {
                try await cooldownSleep(delayNanoseconds)
            } catch {
                return
            }
            guard !Task.isCancelled else { return }
            await self?.scheduledCooldownExpiryReached(scheduleRevision: scheduleRevision)
        }
    }

    func cancelScheduledCooldownRefetch() {
        cooldownRefetchRevision += 1
        cooldownRefetchTask?.cancel()
        cooldownRefetchTask = nil
    }

    private func scheduledCooldownExpiryReached(scheduleRevision: Int) async {
        guard scheduleRevision == cooldownRefetchRevision else { return }
        await refetchExposure()
        if scheduleRevision == cooldownRefetchRevision {
            cooldownRefetchTask = nil
        }
    }
}
