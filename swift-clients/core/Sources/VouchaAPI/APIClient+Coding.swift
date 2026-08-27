import Foundation

extension APIClient {
    static func makeDecoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let string = try container.decode(String.self)
            if let date = VouchaDateParser.parse(string) {
                return date
            }
            throw DecodingError.dataCorruptedError(
                in: container,
                debugDescription: "Cannot parse date: \(string)"
            )
        }
        return decoder
    }

    public static func makeEncoder() -> JSONEncoder {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }
}

private enum VouchaDateParser {
    private static let iso8601WithFractionalSeconds = Date.ISO8601FormatStyle(includingFractionalSeconds: true)
    private static let iso8601 = Date.ISO8601FormatStyle(includingFractionalSeconds: false)

    static func parse(_ string: String) -> Date? {
        if let date = try? Date(string, strategy: iso8601WithFractionalSeconds) {
            return date
        }
        return try? Date(string, strategy: iso8601)
    }
}
