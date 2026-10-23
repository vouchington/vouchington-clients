import Foundation

public extension SentryEventScrubber {
    static func scrubAddress(_ value: String?) -> String? {
        scrub(SentryOutboundEvent(requestURL: value)).requestURL
    }

    static func scrubField(key: String, value: String) -> String? {
        scrub(SentryOutboundEvent(breadcrumbs: [
            SentryBreadcrumbDraft(category: "http", data: [key: value])
        ])).breadcrumbs.first?.data[key]
    }

    static func keepsNumericField(_ key: String) -> Bool {
        numericTelemetryFields.contains(key.lowercased())
    }

    private static let numericTelemetryFields: Set<String> = [
        "status_code", "request_body_size", "response_body_size", "http.response.status_code"
    ]
}
