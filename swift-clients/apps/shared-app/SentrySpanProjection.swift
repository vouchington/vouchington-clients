import Foundation
import Sentry
import VouchaCore

enum SentrySpanProjection {
    static func scrub(_ span: Span) -> Span? {
        span.spanDescription = SentryEventScrubber.scrubAddress(span.spanDescription)
        let keys = Array(span.data.keys)
        var retained: [String: Any] = [:]
        for key in keys {
            guard let value = span.data[key] else { continue }
            if let text = value as? String, let kept = SentryEventScrubber.scrubField(key: key, value: text) {
                retained[key] = kept
            } else if SentryEventProjection.keepsNumber(key, value) {
                retained[key] = value
            }
        }
        for key in keys where retained[key] == nil {
            span.removeData(key: key)
        }
        for (key, value) in retained {
            span.setData(value: value, key: key)
        }
        return span
    }
}
