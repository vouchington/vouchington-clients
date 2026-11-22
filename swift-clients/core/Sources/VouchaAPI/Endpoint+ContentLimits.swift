public extension Endpoint {
    static func contentLimits() -> Endpoint {
        Endpoint(.GET, path: "/api/v1/content-limits")
    }
}
