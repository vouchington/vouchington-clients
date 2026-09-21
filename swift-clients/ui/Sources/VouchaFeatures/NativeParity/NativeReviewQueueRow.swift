import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeReviewQueueRow: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let item: NativeReviewQueueItem
    let timeZone: TimeZone
    @Bindable
    var viewModel: NativeReviewQueueViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            authoredOrFallback(title: item.post.title, fallback: .nativeSwiftModerationReportsReviewQueueUntitledPost)
                .font(Typography.headline)
            authoredOrFallback(
                title: item.post.markdownPreview,
                fallback: .nativeSwiftModerationReportsReviewQueueNoPreview
            )
            .foregroundStyle(.secondary)
            Text(localized(
                .nativeSwiftModerationReportsReviewQueueAuthor,
                parameters: ["author": item.post
                    .createdById ?? localized(.nativeSwiftModerationReportsReviewQueueAnonymous)]
            ))
            Text(localized(
                .nativeSwiftModerationReportsReviewQueuePostType,
                parameters: ["postType": item.post.postType]
            ))
            if let rootContext {
                Text(localized(
                    .nativeSwiftModerationReportsReviewQueueRootThread,
                    parameters: ["root": rootContext]
                ))
            }
            Text(localized(
                .nativeSwiftModerationReportsReviewQueueCreated,
                parameters: ["date": Self.createdTimestamp(item, locale: nativeUiLocale, timeZone: timeZone)]
            ))
            Text(localized(.nativeModerationSummaryTitle))
            Text(localized(item.clearanceStatus.titleKey))
            Text(localized(dispositionKey))
            if let reasonCodesPresentation {
                Text(reasonCodesPresentation)
            }
            Text(localized(
                .nativeModerationSummaryEvidenceFlaggedCategories,
                numberParameters: [
                    "count": Double(item.post.moderationSummary.evidenceSummary.flaggedCategoryCount)
                ]
            ))
            Text(localized(
                .nativeModerationSummaryEvidenceSignals,
                numberParameters: ["count": Double(item.post.moderationSummary.evidenceSummary.signalCount)]
            ))
            NativeReviewQueueMediaGroup(post: item.post, viewModel: viewModel)
            actionButtons
        }
        .padding(Spacing.md)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 12))
        .accessibilityIdentifier("review-queue-row-\(item.id)")
    }

    static func createdTimestamp(
        _ item: NativeReviewQueueItem,
        locale: Locale,
        timeZone: TimeZone
    ) -> String {
        UiMessages.date(
            item.post.createdAt,
            date: .abbreviated,
            time: .shortened,
            locale: locale,
            timeZone: timeZone
        )
    }

    private var actionButtons: some View {
        HStack(spacing: Spacing.sm) {
            actionButton(.nativeSwiftModerationReportsReviewQueueApprove, .approve)
            actionButton(.nativeSwiftModerationReportsReviewQueueReject, .reject)
            actionButton(.nativeSwiftModerationReportsReviewQueueMarkForReview, .reReview)
                .disabled(viewModel.actionsAreDisabled(for: item.id) || item.clearanceStatus == .inReview)
            if viewModel.isMutating(postId: item.id) {
                ProgressView()
            }
        }
    }

    private func actionButton(_ title: UiMessageKey, _ action: NativeReviewQueueAction) -> some View {
        Button(localized(title)) { Task { await viewModel.perform(action, postId: item.id) } }
            .disabled(viewModel.actionsAreDisabled(for: item.id))
    }

    @ViewBuilder
    private func authoredOrFallback(title: String, fallback: UiMessageKey) -> some View {
        let value = title.trimmingCharacters(in: .whitespacesAndNewlines)
        if value.isEmpty {
            Text(localized(fallback))
        } else {
            Text(value).authoredContentLanguage(
                declared: item.post.declaredLanguage,
                detected: item.post.linguaRsDetectedLanguage
            )
        }
    }

    private var rootContext: String? {
        guard let rootId = item.post.rootId else { return nil }
        return [item.post.rootPostType, item.post.rootSlug, rootId].compactMap { $0 }.joined(separator: " · ")
    }

    private var dispositionKey: UiMessageKey {
        switch item.post.moderationSummary.disposition {
        case .pass?: .nativeModerationSummaryDispositionPass
        case .review?: .nativeModerationSummaryDispositionReview
        case .reject?: .nativeModerationSummaryDispositionReject
        case .incomplete?: .nativeModerationSummaryDispositionIncomplete
        case nil: .nativeModerationSummaryDispositionNone
        }
    }

    private var reasonCodesPresentation: String? {
        let reasonCodes = item.post.moderationSummary.reasonCodes
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        guard !reasonCodes.isEmpty else { return nil }
        return localized(
            .nativeSwiftModerationReportsReason,
            parameters: ["reason": reasonCodes.joined(separator: ", ")]
        )
    }

    private func localized(
        _ key: UiMessageKey,
        parameters: [String: String] = [:],
        numberParameters: [String: Double] = [:]
    ) -> String {
        UiMessages.string(
            UiMessage(key, parameters: parameters, numberParameters: numberParameters),
            locale: nativeUiLocale
        )
    }
}
