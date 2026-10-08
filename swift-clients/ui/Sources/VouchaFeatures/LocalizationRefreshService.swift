import Foundation
import VouchaAPI
import VouchaLocalization

@MainActor
public enum LocalizationRefreshService {
    private struct RequestKey: Hashable {
        let cache: ObjectIdentifier
        let locale: String

        func hash(into hasher: inout Hasher) {
            hasher.combine(cache)
            hasher.combine(locale)
        }
    }

    private struct RequestState {
        var active: Set<UInt64> = []
        var latestApplied: UInt64 = 0
    }

    private static var nextSequence: UInt64 = 0
    private static var requests: [RequestKey: RequestState] = [:]

    private static func beginRequest(for key: RequestKey) -> UInt64 {
        nextSequence &+= 1
        var state = requests[key] ?? RequestState()
        state.active.insert(nextSequence)
        requests[key] = state
        return nextSequence
    }

    private static func finishRequest(_ sequence: UInt64, for key: RequestKey) {
        guard var state = requests[key] else { return }
        state.active.remove(sequence)
        requests[key] = state.active.isEmpty ? nil : state
    }

    private static func acceptsResponse(_ sequence: UInt64, for key: RequestKey) -> Bool {
        guard var state = requests[key], sequence > state.latestApplied else { return false }
        state.latestApplied = sequence
        requests[key] = state
        return true
    }

    public static func refresh(
        client: APIClient,
        controller: UiLocaleController,
        cache: LocalizationValueCache = .shared,
        clock: @MainActor () -> Date = Date.init
    ) async {
        let locale = controller.locale.rawValue
        guard cache.isExpired(locale: locale, now: clock()) else { return }
        let requestKey = RequestKey(cache: ObjectIdentifier(cache), locale: locale)
        let sequence = beginRequest(for: requestKey)
        defer { finishRequest(sequence, for: requestKey) }
        let etag = cache.etag(for: locale)
        var endpoint = Endpoint.localization(
            consumer: NativeLocalizationSelectors.consumer,
            locales: [locale],
            selectors: NativeLocalizationSelectors.chrome
        )
        if let etag {
            endpoint = endpoint.withHeaders(["If-None-Match": etag])
        }
        do {
            if let batch: LocalizationBatch = try await client.sendAllowingNotModified(endpoint) {
                guard !Task.isCancelled, acceptsResponse(sequence, for: requestKey) else { return }
                cache.apply(
                    locale: locale,
                    revision: batch.revision,
                    ttlSeconds: batch.ttlSeconds,
                    values: batch.flattenedValues(),
                    now: clock()
                )
                controller.noteOverlayRefresh()
            } else {
                guard !Task.isCancelled, acceptsResponse(sequence, for: requestKey) else { return }
                cache.rememberNotModified(locale: locale, now: clock())
            }
        } catch {
            return
        }
    }
}
