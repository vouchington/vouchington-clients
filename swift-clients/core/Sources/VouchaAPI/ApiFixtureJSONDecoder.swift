import Foundation

public extension JSONDecoder {
    static var vouchaFixtureDecoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let string = try container.decode(String.self)
            if let date = (
                ISO8601DateFormatter.vouchaFixtureWithFractionalSeconds.date(from: string)
                    ?? ISO8601DateFormatter.vouchaFixture.date(from: string)
            ) {
                return date
            }
            throw DecodingError.dataCorruptedError(
                in: container,
                debugDescription: "Cannot parse date: \(string)"
            )
        }
        return decoder
    }
}

private extension ISO8601DateFormatter {
    static let vouchaFixture: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter
    }()

    static let vouchaFixtureWithFractionalSeconds: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter
    }()
}
