/// Wraps a paginated API response. `T` is the element type.
public struct Page<T: Decodable & Sendable>: Decodable, Sendable {
    public let results: [T]
    public let pageInfo: PageInfo

    public init(results: [T], pageInfo: PageInfo = .init(hasNextPage: false)) {
        self.results = results
        self.pageInfo = pageInfo
    }

    public struct PageInfo: Codable, Sendable {
        public let hasNextPage: Bool
        public let hasPreviousPage: Bool?
        public let endCursor: String?
        public let startCursor: String?

        public init(
            hasNextPage: Bool,
            hasPreviousPage: Bool? = nil,
            endCursor: String? = nil,
            startCursor: String? = nil
        ) {
            self.hasNextPage = hasNextPage
            self.hasPreviousPage = hasPreviousPage
            self.endCursor = endCursor
            self.startCursor = startCursor
        }

        public func encode(to encoder: any Encoder) throws {
            var container = encoder.container(keyedBy: PageInfoCodingKeys.self)
            try container.encode(hasNextPage, forKey: .hasNextPage)
            try container.encodeIfPresent(hasPreviousPage, forKey: .hasPreviousPage)
            try container.encode(endCursor, forKey: .endCursor)
            try container.encode(startCursor, forKey: .startCursor)
        }
    }
}

private enum PageInfoCodingKeys: String, CodingKey {
    case hasNextPage, hasPreviousPage, endCursor, startCursor
}

extension Page: Encodable where T: Encodable {}
