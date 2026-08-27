import Foundation
import Observation
import VouchaAPI
import VouchaLocalization

@MainActor
@Observable
public final class NativeDynamicConfigViewModel {
    public private(set) var namespaces: [DynamicConfigNamespaceSummary] = []
    public private(set) var selectedNamespace: DynamicConfigNamespace?
    public private(set) var history: [DynamicConfigHistoryEntry] = []
    public private(set) var isLoading = false
    public private(set) var isSaving = false
    public private(set) var isSelecting = false
    public private(set) var errorMessage: UiVerbatimText?
    public private(set) var feedbackMessage: UiVerbatimText?
    public var query = ""
    public var drafts: [String: String] = [:]
    public private(set) var validationErrors: [String: UiVerbatimText] = [:]

    let client: APIClient?
    private let onNamespaceUpdated: @MainActor (DynamicConfigNamespace) -> Void
    private var selectionGeneration = UUID()

    public init(
        client: APIClient?,
        onNamespaceUpdated: @escaping @MainActor (DynamicConfigNamespace) -> Void = { _ in }
    ) {
        self.client = client
        self.onNamespaceUpdated = onNamespaceUpdated
    }

    public var filteredNamespaces: [DynamicConfigNamespaceSummary] {
        let term = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !term.isEmpty else { return namespaces }
        return namespaces.filter {
            $0.label.localizedCaseInsensitiveContains(term)
                || $0.namespace.localizedCaseInsensitiveContains(term)
                || $0.description.localizedCaseInsensitiveContains(term)
        }
    }

    public func load() async {
        guard let client else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            let response: DynamicConfigNamespacesResponse = try await client.send(.dynamicConfigNamespaces)
            namespaces = response.namespaces
            if selectedNamespace == nil, let first = response.namespaces.first {
                await select(first.namespace)
            }
        } catch {
            errorMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsLoadFailed)
        }
    }

    public func select(_ namespace: String) async {
        guard let client else { return }
        let generation = UUID()
        selectionGeneration = generation
        isSelecting = true
        defer {
            if selectionGeneration == generation {
                isSelecting = false
            }
        }
        errorMessage = nil
        do {
            async let namespaceResponse: DynamicConfigNamespaceResponse = client
                .send(.dynamicConfigNamespace(namespace))
            async let historyResponse: DynamicConfigHistoryResponse = client.send(.dynamicConfigHistory(namespace))
            let (detail, history) = try await (namespaceResponse, historyResponse)
            guard selectionGeneration == generation else { return }
            selectedNamespace = detail.namespace
            self.history = history.history
            drafts = Self.initialDrafts(for: detail.namespace)
            validationErrors = [:]
            feedbackMessage = nil
        } catch {
            guard selectionGeneration == generation else { return }
            errorMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsLoadFailed)
        }
    }

    public func saveBoolean(field: DynamicConfigField, value: Bool) async {
        await save(field: field, value: .boolean(value))
    }

    public func saveDraft(field: DynamicConfigField) async {
        guard let draft = drafts[field.name], let value = validatedValue(draft, for: field) else { return }
        await save(field: field, value: value)
    }

    func setValidationError(_ message: UiVerbatimText?, for fieldName: String) {
        validationErrors[fieldName] = message
    }
}

extension NativeDynamicConfigViewModel {
    private func save(field: DynamicConfigField, value: DynamicConfigValue) async {
        guard let client, let namespace = selectedNamespace, namespace.canUpdate, !isSaving, !isSelecting
        else {
            return
        }
        let generation = selectionGeneration
        isSaving = true
        defer { isSaving = false }
        errorMessage = nil
        feedbackMessage = nil
        do {
            let response: DynamicConfigUpdateResponse = try await client.send(
                .updateDynamicConfigNamespace(namespace.namespace, field: field.name, value: value)
            )
            onNamespaceUpdated(response.namespace)
            guard selectionGeneration == generation,
                  selectedNamespace?.namespace == namespace.namespace
            else {
                return
            }
            selectedNamespace = response.namespace
            drafts = Self.mergingDrafts(drafts, with: response.namespace, savedField: field.name)
            if response.changed {
                feedbackMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsUpdated)
                do {
                    let historyResponse: DynamicConfigHistoryResponse = try await client.send(
                        .dynamicConfigHistory(namespace.namespace)
                    )
                    guard selectionGeneration == generation,
                          selectedNamespace?.namespace == namespace.namespace
                    else {
                        return
                    }
                    history = historyResponse.history
                } catch {
                    if selectionGeneration == generation,
                       selectedNamespace?.namespace == namespace.namespace {
                        errorMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsUpdatedHistoryFailed)
                    }
                }
            } else {
                feedbackMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsNoChange)
            }
        } catch {
            if selectionGeneration == generation,
               selectedNamespace?.namespace == namespace.namespace {
                errorMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsUpdateFailed)
            }
        }
    }

    private static func initialDrafts(for namespace: DynamicConfigNamespace) -> [String: String] {
        namespace.fields.reduce(into: [:]) { result, field in
            guard field.type != .boolean else { return }
            result[field.name] = field.value.editableValue
        }
    }

    private static func mergingDrafts(
        _ drafts: [String: String],
        with namespace: DynamicConfigNamespace,
        savedField: String
    ) -> [String: String] {
        namespace.fields.reduce(into: [:]) { result, field in
            guard field.type != .boolean else { return }
            result[field.name] = field.name == savedField
                ? field.value.editableValue
                : drafts[field.name] ?? field.value.editableValue
        }
    }
}
