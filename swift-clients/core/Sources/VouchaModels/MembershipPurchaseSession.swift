import Foundation

public struct MembershipPurchaseSession: Sendable {
    let transport: any MembershipPurchaseTransport
    private let store: any MembershipStoreAdapter
    private let outbox: any MembershipEvidenceOutbox
    private let keys: any MembershipIdempotencyKeys
    let clock: any MembershipPurchaseClock
    let signals: MembershipPurchaseSignals

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
        if let refusal = NativeMembershipCatalog.admissionRefusal(
            provider: provider,
            productId: productId,
            store: surface,
            products: products,
            overview: overview
        ) {
            return outcome(key: "", refusal: refusal)
        }
        let key = keys.next()
        let intent = try await createIntent(key: key, productId: productId, provider: provider)
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
            try await refreshMembership()
            return initial
        }
        return try await poll(id: initial.id)
    }
}

extension MembershipPurchaseSession {
    private func createIntent(
        key: String,
        productId: String,
        provider: String
    ) async throws -> MembershipIntentSubmission {
        let body = NativeMembershipIntentBody(idempotencyKey: key, productId: productId, provider: provider)
        do {
            return try await transport.createIntent(body)
        } catch is CancellationError {
            throw CancellationError()
        } catch {
            return try await transport.createIntent(body)
        }
    }

    func refreshMembership() async throws {
        do {
            _ = try await transport.membership()
        } catch is CancellationError {
            throw CancellationError()
        } catch {
            return
        }
    }

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
