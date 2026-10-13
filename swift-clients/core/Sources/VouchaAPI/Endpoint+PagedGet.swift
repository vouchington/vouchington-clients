import Foundation

extension Endpoint {
    /// GET whose query starts with `limit` when it is present, then `after` only when a cursor is present.
    /// A nil limit is omitted. Extra items stay after that pair so existing filters keep their order.
    static func pagedGet(
        path: String,
        after: String?,
        limit: Int?,
        queryItems additionalQueryItems: [URLQueryItem] = []
    ) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if let limit {
            queryItems.append(URLQueryItem(name: "limit", value: "\(limit)"))
        }
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        queryItems.append(contentsOf: additionalQueryItems)
        return Endpoint(.GET, path: path, queryItems: queryItems)
    }
}
