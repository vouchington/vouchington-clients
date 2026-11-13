import Foundation
import VouchaAPI

/// The classifier-result and export-request ranges share one request shape but have distinct copy.
enum NativePostgresqlRangeFamily: CaseIterable {
    case classifier
    case request
}

/// Editable UTC date text (`yyyy-MM-dd`) and optional DEFAULT-row budget for one family.
struct NativePostgresqlRangeDraft: Equatable {
    var fromDate = ""
    var toDate = ""
    var maxDefaultRows = ""
}

enum NativePostgresqlRangeError: Equatable {
    case invalidRange
    case invalidJobType
}

struct NativePostgresqlRangeConfirmation {
    let family: NativePostgresqlRangeFamily
    let type: EngineeringPsqlJobType
    let range: EngineeringClassifierRange
}

@MainActor
extension NativeEngineeringPostgresqlViewModel {
    var isRangeAuthorized: Bool {
        rangeAccountId != nil && rangeIsAdministrator
    }

    func updateRangeAccess(accountId: String?, isAdministrator: Bool) {
        guard accountId != rangeAccountId || isAdministrator != rangeIsAdministrator else { return }
        cancelRangeOperation()
        rangeAccountId = accountId
        rangeIsAdministrator = isAdministrator
        rangeQueued = false
        rangeError = nil
    }

    func cancelRangeOperation() {
        rangeGeneration = UUID()
        rangeRequest?.cancel()
        rangeRequest = nil
        rangeConfirmation = nil
        rangeConfirmRequested = false
        actionLoadingKeys.remove("partition-range")
    }

    /// Marks the confirm button as tapped so the dialog's own dismissal does not cancel the selection.
    func requestRangeConfirmation() {
        rangeConfirmRequested = rangeConfirmation != nil
    }

    func dismissRangeConfirmation() {
        guard !rangeConfirmRequested else { return }
        cancelRangeOperation()
    }

    /// Freezes the family's draft into a create or retire selection; retire never sends a row budget.
    func prepareRange(family: NativePostgresqlRangeFamily, retire: Bool) {
        let draft = rangeDrafts[family] ?? NativePostgresqlRangeDraft()
        var rows: Int?
        if !retire, !draft.maxDefaultRows.isEmpty {
            guard draft.maxDefaultRows.utf8.allSatisfy({ (48 ... 57).contains($0) }),
                  let parsed = Int(draft.maxDefaultRows) else {
                prepareRange(
                    family: family,
                    type: .createPartitions,
                    range: EngineeringClassifierRange(fromDate: "", toDate: "")
                )
                return
            }
            rows = parsed
        }
        prepareRange(
            family: family,
            type: retire ? .cleanupPartitions : .createPartitions,
            range: EngineeringClassifierRange(
                fromDate: draft.fromDate, toDate: draft.toDate, maxDefaultRows: rows
            )
        )
    }

    /// Reserves one immutable selection for a native confirmation.
    func prepareRange(
        family: NativePostgresqlRangeFamily,
        type: EngineeringPsqlJobType,
        range: EngineeringClassifierRange
    ) {
        guard isRangeAuthorized, actionLoadingKeys.isEmpty, pendingAction == nil else { return }
        rangeQueued = false
        rangeError = nil
        rangeErrorFamily = family
        guard type == .createPartitions || type == .cleanupPartitions else {
            rangeError = .invalidJobType
            return
        }
        guard range.isValid else {
            rangeError = .invalidRange
            return
        }
        let selection = NativePostgresqlRangeConfirmation(family: family, type: type, range: range)
        rangeConfirmation = selection
        actionLoadingKeys.insert("partition-range")
    }

    /// Consumes the confirmation once; a successful response acknowledges enqueue only.
    func confirmRangeOperation() async {
        guard let selection = rangeConfirmation else { return }
        rangeConfirmRequested = false
        guard isRangeAuthorized, !Task.isCancelled else {
            cancelRangeOperation()
            return
        }
        rangeConfirmation = nil
        let generation = rangeGeneration
        defer {
            if generation == rangeGeneration {
                rangeConfirmation = nil
                rangeRequest = nil
                actionLoadingKeys.remove("partition-range")
            }
        }
        let endpoint: Endpoint = switch selection.family {
        case .classifier: .psqlJobs(type: selection.type, classifierRange: selection.range)
        case .request: .psqlJobs(type: selection.type, requestRange: selection.range)
        }
        let request = Task<EngineeringSuccessResponse, Error> {
            try await client.send(endpoint)
        }
        rangeRequest = request
        do {
            let response = try await withTaskCancellationHandler {
                try await request.value
            } onCancel: {
                request.cancel()
            }
            guard !Task.isCancelled, generation == rangeGeneration, isRangeAuthorized else { return }
            rangeQueued = response.success
            actionErrorMessage = nil
        } catch {
            guard !Task.isCancelled, generation == rangeGeneration, isRangeAuthorized else { return }
            actionErrorMessage = engineeringActionErrorMessage(for: error)
        }
    }
}
