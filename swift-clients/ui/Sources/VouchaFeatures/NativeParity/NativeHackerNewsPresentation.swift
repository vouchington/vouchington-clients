import Foundation
import VouchaAPI
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
