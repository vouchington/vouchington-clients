import Foundation

extension Endpoint {
    /// GET with `limit` first and `after` only when a cursor is present.
    /// Extra items stay after that pair so existing filters keep their order.
    static func pagedGet(
        path: String,
        after: String?,
        limit: Int,
        queryItems additionalQueryItems: [URLQueryItem] = []
    ) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        queryItems.append(contentsOf: additionalQueryItems)
        return Endpoint(.GET, path: path, queryItems: queryItems)
    }
}
