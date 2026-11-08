import Foundation

public struct EngineeringClassifierRange: Encodable, Sendable, Hashable {
    public let fromDate: String
    public let toDate: String
    public let maxDefaultRows: Int?
    public init(fromDate: String, toDate: String, maxDefaultRows: Int? = nil) {
        self.fromDate = fromDate
        self.toDate = toDate
        self.maxDefaultRows = maxDefaultRows
    }
}

public extension EngineeringClassifierRange {
    /// Both bounds are UTC calendar dates; the upper bound is exclusive.
    var isValid: Bool {
        Self.isValidDate(fromDate) && Self.isValidDate(toDate) && fromDate < toDate
            && (maxDefaultRows.map { (1 ... 5_000).contains($0) } ?? true)
    }

    private static func isValidDate(_ value: String) -> Bool {
        let bytes = Array(value.utf8)
        guard bytes.count == 10, bytes[4] == 45, bytes[7] == 45,
              bytes.enumerated().allSatisfy({ index, byte in
                  index == 4 || index == 7 || (48 ... 57).contains(byte)
              }),
              let year = Int(value.prefix(4)), year >= 1_970,
              let month = Int(value.dropFirst(5).prefix(2)),
              let day = Int(value.suffix(2)) else { return false }
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .gmt
        let components = DateComponents(year: year, month: month, day: day)
        guard let date = calendar.date(from: components) else { return false }
        let actual = calendar.dateComponents([.year, .month, .day], from: date)
        return actual.year == year && actual.month == month && actual.day == day
    }
}

/// Export-request and classifier-result families share the same wire range shape.
public typealias EngineeringRequestRange = EngineeringClassifierRange
