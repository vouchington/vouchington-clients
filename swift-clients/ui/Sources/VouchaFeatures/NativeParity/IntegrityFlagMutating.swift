import Foundation
import VouchaModels

protocol IntegrityResolvableFlag: Identifiable where ID == String {
    var resolvedAt: Date? { get }
}

extension VoteIntegrityFlag: IntegrityResolvableFlag {}
extension ReportIntegrityFlag: IntegrityResolvableFlag {}

@MainActor
protocol IntegrityFlagMutating: AnyObject {
    associatedtype Flag: IntegrityResolvableFlag

    var canAccess: Bool { get }
    var flags: [Flag] { get set }
    var selectedStatus: IntegrityStatusFilter { get set }
    var isLoading: Bool { get set }
    var isLoadingMore: Bool { get set }
    var loadGeneration: Int { get set }
    var mutatingFlagIds: Set<String> { get set }
    var reconcilingFlagIds: Set<String> { get set }
    var reconciliationRequiredFlagIds: Set<String> { get set }
    var mutationErrorMessages: [String: String] { get set }

    func reconcile(_ flagId: String) async
}

extension IntegrityFlagMutating {
    func withMutation(_ flagId: String, operation: () async throws -> Void) async {
        guard mutatingFlagIds.insert(flagId).inserted else { return }
        mutationErrorMessages[flagId] = nil
        defer { mutatingFlagIds.remove(flagId) }
        do {
            try await operation()
        } catch {
            guard integrityMutationIsAmbiguous(error) else {
                mutationErrorMessages[flagId] = integrityErrorMessage(error)
                return
            }
            reconciliationRequiredFlagIds.insert(flagId)
            mutationErrorMessages[flagId] = integrityErrorMessage(error)
            mutatingFlagIds.remove(flagId)
            await reconcile(flagId)
        }
    }

    func confirm(_ flag: Flag) {
        loadGeneration += 1
        isLoading = false
        isLoadingMore = false
        let matches = selectedStatus == .all
            || (selectedStatus == .pending) == (flag.resolvedAt == nil)
        guard matches else {
            flags.removeAll { $0.id == flag.id }
            return
        }
        if let index = flags.firstIndex(where: { $0.id == flag.id }) {
            flags[index] = flag
        } else {
            flags.insert(flag, at: 0)
        }
    }

    func canOperate(_ flag: Flag) -> Bool {
        canAccess && flag.resolvedAt == nil && flags.contains { $0.id == flag.id }
            && !mutatingFlagIds.contains(flag.id) && !reconcilingFlagIds.contains(flag.id)
            && !reconciliationRequiredFlagIds.contains(flag.id)
    }
}

extension ReportIntegrityViewModel: IntegrityFlagMutating {}
extension VoteIntegrityViewModel: IntegrityFlagMutating {}
