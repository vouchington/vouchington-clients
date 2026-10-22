import Foundation

public struct MembershipPurchaseSession: Sendable {
    private let transport: any MembershipPurchaseTransport
    private let store: any MembershipStoreAdapter
    private let outbox: any MembershipEvidenceOutbox
    private let keys: any MembershipIdempotencyKeys
    private let clock: any MembershipPurchaseClock
    private let signals: MembershipPurchaseSignals

    public init(
        transport: any MembershipPurchaseTransport,
        store: any MembershipStoreAdapter,
        outbox: any MembershipEvidenceOutbox,
        keys: any MembershipIdempotencyKeys,
        clock: any MembershipPurchaseClock,
        signals: MembershipPurchaseSignals = MembershipPurchaseSignals()
    ) {
        self.transport = transport
        self.store = store
        self.outbox = outbox
        self.keys = keys
        self.clock = clock
        self.signals = signals
    }

    public func purchase(
        productId: String,
        provider: String,
        surface: NativeMembershipStore,
        products: [MembershipCatalogProduct],
        overview: NativeMembershipOverview
    ) async throws -> MembershipPurchaseOutcome {
        let key = keys.next()
        let intent = try await transport.createIntent(
            NativeMembershipIntentBody(idempotencyKey: key, productId: productId, provider: provider)
        )
        switch intent {
        case let .conflict(conflict):
            return outcome(
                key: key,
                refusal: .conflict,
                eligibleAt: conflict.eligibleAt,
                destination: conflict.management.destination
            )
        case let .created(response):
            return try await finish(
                key: key,
                response: response,
                surface: surface,
                products: products,
                overview: overview
            )
        }
    }

    private func finish(
        key: String,
        response: NativeMembershipPurchaseIntentResponse,
        surface: NativeMembershipStore,
        products: [MembershipCatalogProduct],
        overview: NativeMembershipOverview
    ) async throws -> MembershipPurchaseOutcome {
        let intent = response.purchaseIntent
        if let refusal = NativeMembershipCatalog.refusal(
            for: intent,
            products: products,
            store: surface,
            overview: overview
        ) {
            return outcome(key: key, refusal: refusal)
        }
        // The launch, including app_account_token, is forwarded unchanged.
        let purchased = try await store.purchase(launch: intent.launch)
        let body = NativeMembershipVerificationSubmission(
            evidence: purchased.evidence,
            idempotencyKey: key,
            provider: intent.provider,
            purchaseIntentId: intent.id
        )
        await outbox.save(key: key, evidence: purchased.evidence)
        let submitted = try await submit(body)
        await outbox.remove(key: key)
        let verification = try await resolve(submitted)
        return outcome(
            key: key,
            sheetRequested: true,
            status: verification.status,
            reason: verification.reasonCode,
            replayed: submitted.replayed
        )
    }

    private func submit(
        _ body: NativeMembershipVerificationSubmission
    ) async throws -> MembershipVerificationSubmissionResult {
        do {
            return try await transport.submitVerification(body)
        } catch is CancellationError {
            throw CancellationError()
        } catch {
            return try await transport.submitVerification(body)
        }
    }

    private func resolve(
        _ submitted: MembershipVerificationSubmissionResult
    ) async throws -> NativeMembershipVerification {
        let initial = submitted.verification
        if MembershipVerificationPolling.isTerminal(status: initial.status, reasonCode: initial.reasonCode) {
            return initial
        }
        return try await poll(id: initial.id)
    }

    private func poll(id: String) async throws -> NativeMembershipVerification {
        var attempt = 0
        while attempt < 8 {
            let status = try await transport.verification(id: id).verification
            let current = try await transport.membership()
            if MembershipVerificationPolling.isTerminal(status: status.status, reasonCode: status.reasonCode) {
                return status
            }
            let next = current.pending.verifications.first { $0.id == id }?.nextProcessingAt
            let seconds = signals.consumeRefresh()
                ? 0
                : MembershipVerificationPolling.delaySeconds(
                    attempt: attempt,
                    nextProcessingAt: next,
                    now: clock.now
                )
            if seconds > 0 {
                await clock.sleep(seconds: seconds)
            }
            attempt += 1
        }
        throw MembershipPurchaseError.verificationDidNotFinish
    }
}

extension MembershipPurchaseSession {
    private func outcome(
        key: String,
        refusal: MembershipPurchaseRefusal? = nil,
        eligibleAt: String? = nil,
        destination: String? = nil,
        sheetRequested: Bool = false,
        status: String? = nil,
        reason: String? = nil,
        replayed: Bool = false
    ) -> MembershipPurchaseOutcome {
        MembershipPurchaseOutcome(
            sheetRequested: sheetRequested,
            idempotencyKey: key,
            refusal: refusal,
            eligibleAt: eligibleAt,
            managementDestination: destination,
            verificationStatus: status,
            verificationReason: reason,
            replayed: replayed
        )
    }
}
