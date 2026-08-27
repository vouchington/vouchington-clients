import VouchaLocalization
import VouchaModels

extension RssFeedSource {
    var cardFeedTypeLabel: UiVerbatimText {
        switch feedType {
        case "article": .message(.nativeSwiftDesignSystemNews)
        case "podcast": .message(.nativeSwiftDesignSystemPodcast)
        case "video": .message(.nativeSwiftDesignSystemVideo)
        case "mixed": .message(.nativeSwiftDesignSystemMixed)
        default: .verbatim(feedType)
        }
    }

    var cardFeedTypeSystemImage: String {
        switch feedType {
        case "article": "newspaper"
        case "podcast": "headphones"
        case "video": "play.rectangle"
        default: "antenna.radiowaves.left.and.right"
        }
    }
}
