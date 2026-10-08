import Foundation

public extension Endpoint {
    static func localization(
        consumer: String,
        locales: [String],
        selectors: [String]
    ) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/localization",
            queryItems: [
                .init(name: "consumer", value: consumer),
                .init(name: "locales", value: locales.joined(separator: ",")),
                .init(name: "selectors", value: selectors.joined(separator: ","))
            ]
        )
    }
}
