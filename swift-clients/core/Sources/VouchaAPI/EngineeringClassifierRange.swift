public struct EngineeringClassifierRange: Encodable, Sendable {
    public let fromDate: String
    public let toDate: String
    public let maxDefaultRows: Int
    public init(fromDate: String, toDate: String, maxDefaultRows: Int) {
        self.fromDate = fromDate
        self.toDate = toDate
        self.maxDefaultRows = maxDefaultRows
    }
}
