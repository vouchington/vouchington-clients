import Foundation

enum ModerationReportsTestFixtures {
    static let staffFlat = ApiFixtureLoader.data("native.moderation.reports.default")
    static let memberFlat = ApiFixtureLoader.data("native.moderation.reports.member.default")
    static let clustered = ApiFixtureLoader.data("native.moderation.reports.clustered.default")
    static let resolution = ApiFixtureLoader.data("native.moderation.report-resolution.reviewed")
    static let judgement = ApiFixtureLoader.data("native.moderation.report-judgement.default")
    static let warning = ApiFixtureLoader.data("native.moderation.admin-warning.report")

    static func staffFlat(
        ids: [String],
        systemIds: Set<String> = [],
        targetIds: [String: String] = [:],
        statuses: [String: String] = [:],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) throws -> Data {
        var root = try object(staffFlat)
        let prototype = try reportPrototype(root)
        root["results"] = ids.map { id in
            var report = prototype
            report["id"] = id
            report["entity_id"] = targetIds[id] ?? "post-\(id)"
            report["status"] = statuses[id] ?? "pending"
            if systemIds.contains(id) {
                report["entity_type"] = "user"
                report["entity_id"] = "user-2"
                report["is_system_generated"] = true
                report["community_ban_evasion"] = banEvasionContext
            }
            return report
        }
        root["page_info"] = pageInfo(hasNextPage: hasNextPage, endCursor: endCursor)
        return try data(root)
    }

    static func member(ids: [String], hasNextPage: Bool = false, endCursor: String? = nil) throws -> Data {
        var root = try object(memberFlat)
        let prototype = try reportPrototype(root)
        root["results"] = ids.map { id in
            var report = prototype
            report["id"] = id
            report["entity_id"] = "post-\(id)"
            return report
        }
        root["page_info"] = pageInfo(hasNextPage: hasNextPage, endCursor: endCursor)
        return try data(root)
    }

    static func cluster(
        reportIds: [String],
        systemIds: Set<String> = [],
        statuses: [String: String] = [:],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) throws -> Data {
        var root = try object(clustered)
        var clusters = root["results"] as? [[String: Any]] ?? []
        guard var cluster = clusters.first,
              let prototype = (cluster["reports"] as? [[String: Any]])?.first
        else { throw FixtureError.missingClusterReport }
        let reports = reportIds.map { id in
            var report = prototype
            report["id"] = id
            report["status"] = statuses[id] ?? "pending"
            if systemIds.contains(id) {
                report["entity_type"] = "user"
                report["entity_id"] = "user-2"
                report["is_system_generated"] = true
                report["community_ban_evasion"] = banEvasionContext
            }
            return report
        }
        cluster["reports"] = reports
        cluster["report_count"] = reports.count
        clusters[0] = cluster
        root["results"] = clusters
        root["page_info"] = pageInfo(hasNextPage: hasNextPage, endCursor: endCursor)
        return try data(root)
    }

    static func partialCluster(
        reportIds: [String],
        systemIds: Set<String> = [],
        reportCount: Int,
        reporterCount: Int,
        reasons: [String: Int]
    ) throws -> Data {
        var root = try object(cluster(reportIds: reportIds, systemIds: systemIds))
        var clusters = root["results"] as? [[String: Any]] ?? []
        guard !clusters.isEmpty else { throw FixtureError.missingCluster }
        clusters[0]["report_count"] = reportCount
        clusters[0]["reporter_count"] = reporterCount
        clusters[0]["reason_breakdown"] = reasons.map { ["reason": $0.key, "count": $0.value] }
        root["results"] = clusters
        return try data(root)
    }

    static func nestedDuplicate(
        reportIds: [String],
        reportCount: Int? = nil,
        includeTopLevelClusters: Bool = false,
        duplicateId: String = "duplicate-content:native-posts",
        signal: String? = nil,
        systemIds: Set<String> = [],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) throws -> Data {
        var root = try object(clustered)
        guard var duplicate = (root["duplicate_clusters"] as? [[String: Any]])?.first,
              var nestedCluster = (duplicate["clusters"] as? [[String: Any]])?.first,
              let prototype = (nestedCluster["reports"] as? [[String: Any]])?.first
        else { throw FixtureError.missingDuplicateClusterReport }
        nestedCluster["reports"] = reportIds.map { id in
            var report = prototype
            report["id"] = id
            report["entity_id"] = "post-\(id)"
            if systemIds.contains(id) {
                report["entity_type"] = "user"
                report["entity_id"] = "user-2"
                report["is_system_generated"] = true
                report["community_ban_evasion"] = banEvasionContext
            }
            return report
        }
        nestedCluster["report_count"] = reportCount ?? reportIds.count
        duplicate["clusters"] = [nestedCluster]
        duplicate["report_count"] = reportCount ?? reportIds.count
        duplicate["post_count"] = max(reportIds.count, 1)
        duplicate["id"] = duplicateId
        if let signal {
            duplicate["signal"] = signal
        }
        root["duplicate_clusters"] = [duplicate]
        root["page_info"] = pageInfo(hasNextPage: hasNextPage, endCursor: endCursor)
        if !includeTopLevelClusters {
            root["results"] = []
        }
        return try data(root)
    }

    private static var banEvasionContext: [String: Any] {
        [
            "community_id": "community-1",
            "community_slug": "native-community",
            "source_user_id": "banned-user",
            "source_username": "banned",
            "score": 0.85,
            "flagged_at": "2026-06-01T12:00:00.000Z"
        ]
    }

    private static func object(_ data: Data) throws -> [String: Any] {
        guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw FixtureError.invalidRoot
        }
        return root
    }

    private static func reportPrototype(_ root: [String: Any]) throws -> [String: Any] {
        guard let report = (root["results"] as? [[String: Any]])?.first else {
            throw FixtureError.missingReport
        }
        return report
    }

    private static func pageInfo(hasNextPage: Bool, endCursor: String?) -> [String: Any] {
        ["has_next_page": hasNextPage, "end_cursor": endCursor ?? NSNull()]
    }

    private static func data(_ object: [String: Any]) throws -> Data {
        try JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])
    }

    private enum FixtureError: Error {
        case invalidRoot
        case missingCluster
        case missingClusterReport
        case missingDuplicateClusterReport
        case missingReport
    }
}
