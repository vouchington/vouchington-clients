import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class PointValuationsViewModel {
    var pagination = CursorPaginationState<PointValuation>()
    var valuations: [PointValuation] {
        get { pagination.items }
        set { pagination.replaceItems(reconciled(newValue)) }
    }

    var topicQuery = "" {
        didSet {
            guard topicQuery != oldValue else { return }
            searchGeneration = UUID()
            topicResults = []
            searchErrorMessage = nil
            isSearching = false
        }
    }

    var topicResults: [TopicSearchResult] = []
    var isLoading = false
    var isSearching = false
    var isCreating = false
    var mutatingIds: Set<String> = []
    var errorMessage: UiVerbatimText?
    var searchErrorMessage: UiVerbatimText?
    var mutationErrorMessage: UiVerbatimText?
    var continuationErrorMessage: UiVerbatimText? {
        guard pagination.hasLoadedPage, pagination.lastError != nil, errorMessage == nil else { return nil }
        return .message(.nativeSwiftCommonTryAgain)
    }

    let service: any PointValuationServicing
    private var localUpserts: [String: PointValuation] = [:]
    private var deletedIds: Set<String> = []
    var searchGeneration = UUID()

    init(service: any PointValuationServicing) {
        self.service = service
    }

    func create(rewardsProgramId: String, draft: PointValuationDraft) async -> Bool {
        guard !isCreating, canCreate(rewardsProgramId: rewardsProgramId) else { return false }
        let body: CreatePointValuationBody
        do {
            body = try draft.createBody(rewardsProgramId: rewardsProgramId)
        } catch {
            mutationErrorMessage = .message(.extractedMyPointValuationsManagerPleaseEnterAvalidValuePerPoint67b38bc3)
            return false
        }
        isCreating = true
        mutationErrorMessage = nil
        defer { isCreating = false }
        do {
            try await upsert(service.create(body: body))
            return true
        } catch {
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func save(valuation: PointValuation, draft: PointValuationDraft) async -> Bool {
        guard !mutatingIds.contains(valuation.id) else { return false }
        let body: UpdatePointValuationBody
        do {
            guard let changes = try draft.updateBody(comparedWith: valuation) else { return true }
            body = changes
        } catch {
            mutationErrorMessage = .message(.extractedMyPointValuationsManagerPleaseEnterAvalidValuePerPoint67b38bc3)
            return false
        }
        mutatingIds.insert(valuation.id)
        mutationErrorMessage = nil
        defer { mutatingIds.remove(valuation.id) }
        do {
            try await upsert(service.update(id: valuation.id, body: body))
            return true
        } catch {
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func delete(_ valuation: PointValuation) async -> Bool {
        guard !mutatingIds.contains(valuation.id),
              let rank = valuations.firstIndex(where: { $0.id == valuation.id })
        else { return false }
        mutatingIds.insert(valuation.id)
        mutationErrorMessage = nil
        deletedIds.insert(valuation.id)
        let removedLocalUpsert = localUpserts.removeValue(forKey: valuation.id)
        pagination.replaceItems(valuations.filter { $0.id != valuation.id })
        defer { mutatingIds.remove(valuation.id) }
        do {
            try await service.delete(id: valuation.id)
            return true
        } catch {
            deletedIds.remove(valuation.id)
            if let removedLocalUpsert {
                localUpserts[valuation.id] = removedLocalUpsert
            }
            var restored = valuations.filter { $0.id != valuation.id }
            restored.insert(valuation, at: min(rank, restored.count))
            pagination.replaceItems(restored)
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func reconciled(_ serverValues: [PointValuation]) -> [PointValuation] {
        var values = Dictionary(serverValues.map { ($0.id, $0) }, uniquingKeysWith: { _, latest in latest })
        values.merge(localUpserts, uniquingKeysWith: { _, local in local })
        return values.values.filter { !deletedIds.contains($0.id) }.sorted { $0.id < $1.id }
    }

    func canCreate(rewardsProgramId: String) -> Bool {
        !valuations.contains { $0.rewardsProgramId == rewardsProgramId }
    }

    private func upsert(_ valuation: PointValuation) {
        deletedIds.remove(valuation.id)
        localUpserts[valuation.id] = valuation
        valuations = valuations.filter { $0.id != valuation.id } + [valuation]
        mutationErrorMessage = nil
    }
}
