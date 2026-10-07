import Foundation
import VouchaAPI
import VouchaLocalization

@MainActor
public enum LocalizationRefreshService {
    public static func refresh(
        client: APIClient,
        controller: UiLocaleController,
        cache: LocalizationValueCache = .shared,
        now: Date = Date()
    ) async {
        let locale = controller.locale.rawValue
        guard cache.isExpired(locale: locale, now: now) else { return }
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
                cache.apply(
                    locale: locale,
                    revision: batch.revision,
                    ttlSeconds: batch.ttlSeconds,
                    values: batch.flattenedValues(),
                    now: now
                )
                controller.noteOverlayRefresh()
            } else {
                cache.rememberNotModified(locale: locale, now: now)
            }
        } catch {
            return
        }
    }
}
