import Foundation
import VouchaAPI
import VouchaLocalization

@MainActor
public enum LocalizationRefreshService {
    private struct RequestKey: Hashable {
        let cache: ObjectIdentifier
        let locale: String
    }

    private static var latestRequest: [RequestKey: UUID] = [:]

    public static func refresh(
        client: APIClient,
        controller: UiLocaleController,
        cache: LocalizationValueCache = .shared,
        now: Date = Date()
    ) async {
        let locale = controller.locale.rawValue
        guard cache.isExpired(locale: locale, now: now) else { return }
        let requestKey = RequestKey(cache: ObjectIdentifier(cache), locale: locale)
        let requestID = UUID()
        latestRequest[requestKey] = requestID
        defer {
            if latestRequest[requestKey] == requestID {
                latestRequest.removeValue(forKey: requestKey)
            }
        }
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
                guard latestRequest[requestKey] == requestID, !Task.isCancelled else { return }
                cache.apply(
                    locale: locale,
                    revision: batch.revision,
                    ttlSeconds: batch.ttlSeconds,
                    values: batch.flattenedValues(),
                    now: now
                )
                controller.noteOverlayRefresh()
            } else {
                guard latestRequest[requestKey] == requestID, !Task.isCancelled else { return }
                cache.rememberNotModified(locale: locale, now: now)
            }
        } catch {
            return
        }
    }
}
