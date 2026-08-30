import VouchaLocalization
import VouchaModels

extension NativePostComposeViewModel {
    var draftRow: NativeRouteDestinationRow {
        switch postType {
        case .link:
            .init(
                icon: "link",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledLink)
                    : .verbatim(title),
                detail: linkURL.trimmedOrNil.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftPostComposeSavedLinkDraft)
            )
        case .review:
            .init(
                icon: "star",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledReview)
                    : .verbatim(title),
                detail: .count(reviewTopicRatings.count, item: "rating")
            )
        case .dataPoint:
            .init(
                icon: "chart.bar",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledDataPoint)
                    : .verbatim(title),
                detail: dataPointVertical.map { .message($0.titleKey) }
                    ?? .message(.nativeSwiftPostComposeSavedDataPointDraft)
            )
        default:
            .init(
                icon: "tray.and.arrow.down",
                title: title.isEmpty
                    ? .message(.nativeSwiftPostComposeUntitledDraft)
                    : .verbatim(title),
                detail: bodyText.trimmedOrNil.map(UiVerbatimText.verbatim)
                    ?? imageSummary
                    ?? .message(.nativeSwiftPostComposeSavedDraft)
            )
        }
    }
}
