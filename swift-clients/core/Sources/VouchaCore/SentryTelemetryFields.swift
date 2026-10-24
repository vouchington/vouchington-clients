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

    static func isSeparateRequestPart(_ name: String) -> Bool {
        name.split(whereSeparator: { !$0.isLetter }).contains { token in
            token.hasPrefix("query") || token.hasPrefix("fragment")
        }
    }

    static func retainedValue(_ key: String, _ value: String) -> String {
        if key.lowercased().contains("url") || value.contains("://") || value.hasPrefix("/") {
            return stripQuery(value) ?? value
        }
        return value
    }

    /// True when a field name carries a secret or personal identifier. Names are split into words
    /// (separators and camelCase) and matched whole, so `author` or `session_duration_ms`-style
    /// telemetry is not caught by substring, while `sessionId`, `X-Auth-Token` and `apiKey` are.
    static func isSensitiveName(_ name: String) -> Bool {
        let words = nameWords(name)
        if words.contains(where: sensitiveWords.contains) { return true }
        return zip(words, words.dropFirst()).contains { sensitiveWordPairs.contains($0 + $1) }
    }

    private static func nameWords(_ name: String) -> [String] {
        var words: [String] = []
        var current = ""
        var previousLower = false
        for character in name {
            guard character.isLetter || character.isNumber else {
                if !current.isEmpty { words.append(current.lowercased()) }
                current = ""
                previousLower = false
                continue
            }
            if character.isUppercase, previousLower {
                words.append(current.lowercased())
                current = ""
            }
            current.append(character)
            previousLower = character.isLowercase
        }
        if !current.isEmpty { words.append(current.lowercased()) }
        return words
    }

    private static let sensitiveWords: Set<String> = [
        "email", "password", "passwd", "passcode", "token", "tokens", "secret", "secrets", "session",
        "auth", "authorization", "cookie", "cookies", "signature", "apikey", "credential", "credentials",
        "jwt", "bearer", "otp"
    ]
    private static let sensitiveWordPairs: Set<String> = ["apikey", "privatekey", "accesskey", "secretkey"]

    private static let numericTelemetryFields: Set<String> = [
        "status_code", "request_body_size", "response_body_size", "http.response.status_code"
    ]
}
