/// Finite `time_range` values accepted by post lists.
/// Native lists explicitly send the last 30 days by default; omission disables the producer age filter.
public enum PostTimeRange: String, CaseIterable, Sendable {
    case lastWeek = "1w"
    case last30Days = "1m"
    case lastYear = "1y"
    case allTime = "all"
}
