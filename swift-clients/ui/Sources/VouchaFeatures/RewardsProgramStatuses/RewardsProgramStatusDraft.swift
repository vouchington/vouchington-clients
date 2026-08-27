import VouchaAPI
import VouchaModels

struct RewardsProgramStatusDraft: Equatable {
    var since: LocalDate?
    var until: LocalDate?

    init(since: LocalDate? = nil, until: LocalDate? = nil) {
        self.since = since
        self.until = until
    }

    init(status: RewardsProgramStatus) {
        self.init(since: status.since, until: status.until)
    }

    var hasValidDateRange: Bool {
        guard let since, let until else { return true }
        return (since.year, since.month, since.day) <= (until.year, until.month, until.day)
    }

    func updateBody(comparedWith status: RewardsProgramStatus) -> UpdateRewardsProgramStatusBody? {
        let sincePatch: NullableValue<LocalDate>? = patch(since, comparedWith: status.since)
        let untilPatch: NullableValue<LocalDate>? = patch(until, comparedWith: status.until)
        guard sincePatch != nil || untilPatch != nil else { return nil }
        return .init(since: sincePatch, until: untilPatch)
    }

    private func patch(_ value: LocalDate?, comparedWith oldValue: LocalDate?) -> NullableValue<LocalDate>? {
        guard value != oldValue else { return nil }
        return value.map(NullableValue.value) ?? .null
    }
}
