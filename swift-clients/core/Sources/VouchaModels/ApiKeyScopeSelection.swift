import Foundation

/// Only individual catalogue entries may be selected. Umbrella entries never expand into resources.
public struct ApiKeyScopeSelection: Sendable {
    private var scopes: [CredentialScope]
    private var type: ApiKeyType
    public private(set) var selectedScopes: Set<String> = []

    public init(scopes: [CredentialScope] = [], type: ApiKeyType = .rss) {
        self.scopes = scopes
        self.type = type
    }

    public var availableScopes: [CredentialScope] {
        guard catalogIsValid else { return [] }
        return scopes.filter { entry in
            entry.surfaces.contains(.apiKey) &&
                !entry.scope.lowercased().hasPrefix("mcp.admin:") &&
                (type == .rss ? entry.audience == .api : entry.audience == .user)
        }.sorted { $0.scope < $1.scope }
    }

    public var permissions: [String] {
        selectedScopes.sorted()
    }

    public mutating func clear() {
        selectedScopes = []
    }

    public var isValid: Bool {
        guard !selectedScopes.isEmpty, type != .rss || selectedScopes.count == 1 else { return false }
        let entries = availableScopes.filter { selectedScopes.contains($0.scope) }
        return entries.count == selectedScopes.count && Set(entries.map(\.audience)).count == 1 &&
            entries.allSatisfy { $0.requires == nil || selectedScopes.contains($0.requires ?? "") }
    }

    public mutating func configure(type: ApiKeyType) {
        if self.type != type {
            selectedScopes = []
        }
        self.type = type
    }

    public mutating func replaceCatalog(_ scopes: [CredentialScope]) {
        self.scopes = scopes
        selectedScopes = selectedScopes.intersection(Set(availableScopes.map(\.scope)))
        if !selectedScopes.isEmpty, !isValid { selectedScopes = [] }
    }

    public func canSelect(_ scope: String) -> Bool {
        guard let entry = availableScopes.first(where: { $0.scope == scope }) else { return false }
        return availableScopes.filter { selectedScopes.contains($0.scope) }
            .allSatisfy { $0.audience == entry.audience }
    }

    @discardableResult
    public mutating func setSelected(_ scope: String, selected: Bool) -> Bool {
        guard availableScopes.contains(where: { $0.scope == scope }) else { return false }
        if selected {
            guard canSelect(scope) else { return false }
            var prerequisites: Set<String> = []
            guard collect(scope, from: availableScopes, visited: &prerequisites) else { return false }
            let candidate = selectedScopes.union(prerequisites)
            guard type != .rss || candidate.count == 1 else { return false }
            selectedScopes = candidate
        } else {
            selectedScopes.remove(scope)
            var removedDependent = true
            while removedDependent {
                let invalid = availableScopes.filter {
                    selectedScopes.contains($0.scope) && $0.requires.map { !selectedScopes.contains($0) } == true
                }
                removedDependent = !invalid.isEmpty
                selectedScopes.subtract(invalid.map(\.scope))
            }
        }
        return true
    }

    private var catalogIsValid: Bool {
        guard Set(scopes.map(\.scope)).count == scopes.count else { return false }
        return scopes.allSatisfy { entry in
            var visited: Set<String> = []
            return collect(entry.scope, from: scopes, visited: &visited)
        }
    }

    private func collect(_ scope: String, from entries: [CredentialScope], visited: inout Set<String>) -> Bool {
        guard !visited.contains(scope), let entry = entries.first(where: { $0.scope == scope }) else { return false }
        visited.insert(scope)
        guard let required = entry.requires else { return true }
        guard let prerequisite = entries.first(where: { $0.scope == required }),
              prerequisite.audience == entry.audience,
              Set(entry.surfaces).isSubset(of: Set(prerequisite.surfaces)) else { return false }
        return collect(required, from: entries, visited: &visited)
    }
}
