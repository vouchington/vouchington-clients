import Foundation
import VouchaLocalization
import VouchaModels

func moderationTransparencyRows(
    _ buckets: [ModerationTransparencyBucket],
    range: ModerationTransparencyRange
) -> [NativeRouteDestinationRow] {
    guard buckets.isEmpty == false else {
        return [.init(
            icon: "shield",
            title: .message(.nativeSwiftCommunityRowsModerationTransparency),
            detail: .message(.nativeSwiftCommunityRowsTransparencyEmpty)
        )]
    }
    return buckets.map { bucket in
        .init(
            id: "\(bucket.date):\(bucket.metric.rawValue):\(bucket.category)",
            icon: "shield",
            title: .joined([
                .message(moderationTransparencyTitle(bucket.metric)),
                .message(moderationTransparencyCategoryTitle(bucket.category))
            ]),
            detail: .message(
                .nativeSwiftCommunityRowsTransparencyBucketDetail,
                textParameters: [
                    "date": moderationTransparencyReleasedOn(bucket.date, range: range),
                    "category": .message(moderationTransparencyCategoryTitle(bucket.category))
                ],
                numberParameters: ["count": Double(bucket.count)]
            )
        )
    }
}

private func moderationTransparencyReleasedOn(
    _ rawDate: String,
    range: ModerationTransparencyRange
) -> UiVerbatimText {
    let parts = rawDate.split(separator: "-").compactMap { Int($0) }
    guard parts.count == 3 else { return .protocolValue(rawDate) }
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = .current
    guard let date = calendar.date(from: DateComponents(
        year: parts[0],
        month: parts[1],
        day: parts[2],
        hour: 12
    )) else { return .protocolValue(rawDate) }
    return .message(
        .nativeSwiftCommunityRowsTransparencyReleasedOn,
        dateParameters: ["date": .init(date, dateStyle: range == .all ? .monthYear : .abbreviated)]
    )
}

private func moderationTransparencyTitle(_ metric: ModerationTransparencyMetric) -> UiMessageKey {
    switch metric {
    case .appeals: .nativeSwiftCommunityRowsAppeals
    case .automatedModeration: .nativeSwiftCommunityRowsAutomod
    case .moderationActions: .nativeSwiftCommunityRowsModerationActions
    case .reports: .nativeSwiftRouteSurfaceReports
    case .unknown: .nativeSwiftCommunityRowsModerationTransparency
    }
}

private let moderationTransparencyCategoryTitles: [String: UiMessageKey] = [
    "accept": .nativeSwiftCommunityRowsTransparencyCategoriesAccept,
    "activate_restriction": .nativeSwiftCommunityRowsTransparencyCategoriesActivateRestriction,
    "agent_moderation": .nativeSwiftCommunityRowsTransparencyCategoriesAgentModeration,
    "approve": .nativeSwiftCommunityRowsTransparencyCategoriesApprove,
    "ban": .nativeSwiftCommunityRowsTransparencyCategoriesBan,
    "change_role": .nativeSwiftCommunityRowsTransparencyCategoriesChangeRole,
    "community_ai": .nativeSwiftCommunityRowsTransparencyCategoriesCommunityAi,
    "deny": .nativeSwiftCommunityRowsTransparencyCategoriesDeny,
    "dismiss_appeal": .nativeSwiftCommunityRowsTransparencyCategoriesDismissAppeal,
    "dismiss_report": .nativeSwiftCommunityRowsTransparencyCategoriesDismissReport,
    "harassment": .nativeSwiftCommunityRowsTransparencyCategoriesHarassment,
    "illegal_content": .nativeSwiftCommunityRowsTransparencyCategoriesIllegalContent,
    "lift_ban": .nativeSwiftCommunityRowsTransparencyCategoriesLiftBan,
    "lift_restriction": .nativeSwiftCommunityRowsTransparencyCategoriesLiftRestriction,
    "lock": .nativeSwiftCommunityRowsTransparencyCategoriesLock,
    "misinformation": .nativeSwiftCommunityRowsTransparencyCategoriesMisinformation,
    "openai_omni": .nativeSwiftCommunityRowsTransparencyCategoriesOpenAiModeration,
    "other": .nativeSwiftCommunityRowsTransparencyCategoriesOther,
    "pin": .nativeSwiftCommunityRowsTransparencyCategoriesPin,
    "post_clearance_reject": .nativeSwiftCommunityRowsTransparencyCategoriesPostClearanceRejection,
    "reduce": .nativeSwiftCommunityRowsTransparencyCategoriesReduce,
    "reject": .nativeSwiftCommunityRowsTransparencyCategoriesReject,
    "remove": .nativeSwiftCommunityRowsTransparencyCategoriesRemove,
    "remove_member": .nativeSwiftCommunityRowsTransparencyCategoriesRemoveMember,
    "resolve_appeal": .nativeSwiftCommunityRowsTransparencyCategoriesResolveAppeal,
    "resolve_report": .nativeSwiftCommunityRowsTransparencyCategoriesResolveReport,
    "spam": .nativeSwiftCommunityRowsTransparencyCategoriesSpam,
    "spam_detection": .nativeSwiftCommunityRowsTransparencyCategoriesSpamDetection,
    "suspend": .nativeSwiftCommunityRowsTransparencyCategoriesSuspend,
    "tag": .nativeSwiftCommunityRowsTransparencyCategoriesTag,
    "unlock": .nativeSwiftCommunityRowsTransparencyCategoriesUnlock,
    "unpin": .nativeSwiftCommunityRowsTransparencyCategoriesUnpin,
    "unsuspend": .nativeSwiftCommunityRowsTransparencyCategoriesUnsuspend,
    "vote_manipulation": .nativeSwiftCommunityRowsTransparencyCategoriesVoteManipulation,
    "warn": .nativeSwiftCommunityRowsTransparencyCategoriesWarn
]

private func moderationTransparencyCategoryTitle(_ category: String) -> UiMessageKey {
    moderationTransparencyCategoryTitles[category] ?? .nativeSwiftCommunityRowsTransparencyCategoriesOther
}
