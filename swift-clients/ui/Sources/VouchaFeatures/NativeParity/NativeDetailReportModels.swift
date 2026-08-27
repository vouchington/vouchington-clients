enum NativeDetailReportTarget: Equatable {
    case user(id: String)
    case urlHostname(id: String)

    var entityType: String {
        switch self {
        case .user: "user"
        case .urlHostname: "url_hostname"
        }
    }

    var entityId: String {
        switch self {
        case let .user(id), let .urlHostname(id): id
        }
    }

    var subjectName: String {
        switch self {
        case .user: "user"
        case .urlHostname: "domain"
        }
    }
}

enum NativeDetailReportSubmissionState: Equatable {
    case idle
    case submitting
    case submitted
}
