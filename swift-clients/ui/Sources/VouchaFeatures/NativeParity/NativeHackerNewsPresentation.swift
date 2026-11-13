import Foundation
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    var supportsHackerNewsFilter: Bool {
        if topicPostCollection != nil { return true }
        return switch destination {
        case .postsBrowse, .storiesBrowse, .feedNews, .feedPodcasts, .feedVideos: true
        default: false
        }
    }

    var isHackerNewsFilterVisible: Bool {
        supportsHackerNewsFilter && hackerNewsReferencesVisible
    }

    /// Refresh the backend flag. A failed request keeps the last known value (hidden initially).
    func refreshHackerNewsReferencesVisibility() async {
        guard let client,
              let visible = await HackerNewsReferencesVisibility.fetch(client: client) else { return }
        let wasVisible = hackerNewsReferencesVisible
        hackerNewsReferencesVisible = visible
        if !visible, hasHackerNewsReferences { hasHackerNewsReferences = false }
        // A route-selected filter now changes the effective query: restart the first page and
        // supersede the unfiltered load so its rows and cursor are discarded.
        if visible, !wasVisible, hasHackerNewsReferences, supportsHackerNewsFilter {
            hackerNewsFilterRevision += 1
            state = .idle
            hackerNewsFilterTask = Task { await load() }
        }
    }

    func hackerNewsDetail(_ detail: UiVerbatimText, references: HackerNewsReferences?) -> UiVerbatimText {
        guard let references else { return detail }
        var parts = [detail]
        if references.storyCount > 0 {
            parts.append(.app(UiMessage(
                .sharedHackerNewsSeenOnCount,
                numberParameters: ["count": Double(references.storyCount)]
            )))
        }
        if references.commentCount > 0 {
            parts.append(.app(UiMessage(
                references.storyCount > 0 ? .sharedHackerNewsMentionedCount : .sharedHackerNewsMentionedOnCount,
                numberParameters: ["count": Double(references.commentCount)]
            )))
        }
        return .composition(parts, separator: " · ")
    }

}
