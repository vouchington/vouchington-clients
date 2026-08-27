import Foundation
import SwiftUI
import VouchaFeatures

struct AppSectionPreferences: Codable, Equatable {
    var orderedSectionIDs: [String]
    var hiddenSectionIDs: Set<String>

    static let `default` = AppSectionPreferences(
        orderedSectionIDs: AppSection.allCases.map(\.rawValue),
        hiddenSectionIDs: []
    )

    var orderedSections: [AppSection] {
        let known = Set(AppSection.allCases.map(\.rawValue))
        let persisted = orderedSectionIDs.compactMap(AppSection.init(rawValue:))
        let missing = AppSection.allCases.filter { !orderedSectionIDs.contains($0.rawValue) }
        let filtered = persisted.filter { known.contains($0.rawValue) }
        return filtered + missing
    }

    var visibleSections: [AppSection] {
        let visible = orderedSections.filter { !hiddenSectionIDs.contains($0.rawValue) }
        return visible.isEmpty ? [.news] : visible
    }

    mutating func normalize() {
        orderedSectionIDs = orderedSections.map(\.rawValue)
        hiddenSectionIDs = hiddenSectionIDs.intersection(Set(AppSection.allCases.map(\.rawValue)))
        if orderedSections.filter({ !hiddenSectionIDs.contains($0.rawValue) }).isEmpty {
            hiddenSectionIDs.remove(AppSection.news.rawValue)
        }
    }

    mutating func setHidden(_ section: AppSection, hidden: Bool) {
        if hidden {
            guard visibleSections.count > 1 else { return }
            hiddenSectionIDs.insert(section.rawValue)
        } else {
            hiddenSectionIDs.remove(section.rawValue)
        }
    }

    mutating func move(fromOffsets source: IndexSet, toOffset destination: Int) {
        var sections = orderedSections
        sections.move(fromOffsets: source, toOffset: destination)
        orderedSectionIDs = sections.map(\.rawValue)
    }
}

enum AppSectionPreferenceStore {
    private static let fileName = "app-section-preferences.json"

    static func load(fileManager: FileManager = .default) -> AppSectionPreferences {
        let url = preferencesURL(fileManager: fileManager)
        guard let data = try? Data(contentsOf: url),
              var preferences = try? JSONDecoder().decode(AppSectionPreferences.self, from: data)
        else {
            return .default
        }
        preferences.normalize()
        return preferences
    }

    static func save(_ preferences: AppSectionPreferences, fileManager: FileManager = .default) {
        let url = preferencesURL(fileManager: fileManager)
        try? fileManager.createDirectory(
            at: url.deletingLastPathComponent(),
            withIntermediateDirectories: true,
            attributes: nil
        )
        guard let data = try? JSONEncoder().encode(preferences) else { return }
        try? data.write(to: url, options: .atomic)
    }

    private static func preferencesURL(fileManager: FileManager) -> URL {
        let base = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? fileManager.temporaryDirectory
        return base.appendingPathComponent("ai.voucha", isDirectory: true).appendingPathComponent(fileName)
    }
}
