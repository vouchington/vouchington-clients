import Foundation
import VouchaAPI

/// Range controls remain unavailable until the producer publishes their presentation contract.
enum NativePostgresqlRangeFamily: CaseIterable {
    case classifier
    case request
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
        actionLoadingKeys.remove("partition-range")
    }

    /// Reserves one immutable selection for a future native confirmation.
    func prepareRange(
        family: NativePostgresqlRangeFamily,
        type: EngineeringPsqlJobType,
        range: EngineeringClassifierRange
    ) {
        guard isRangeAuthorized, actionLoadingKeys.isEmpty, pendingAction == nil else { return }
        rangeQueued = false
        rangeError = nil
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
