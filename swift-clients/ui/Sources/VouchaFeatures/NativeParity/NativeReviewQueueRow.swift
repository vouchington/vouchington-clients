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
            Text(displayTitle).font(Typography.headline)
            Text(displayExcerpt).foregroundStyle(.secondary)
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
            Text(localized(.nativeSwiftModerationReportsReviewQueueStatus, parameters: ["status": statusLabel]))
            Text(localized(.nativeSwiftModerationReportsReviewQueueSpam, parameters: ["spam": spamLabel]))
            Text(localized(
                .nativeSwiftModerationReportsReviewQueueOpenAiModeration,
                parameters: ["status": flaggedLabel(item.post.openaiOmniModerationFlagged == true)]
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

    private var displayTitle: String {
        let title = item.post.title.trimmingCharacters(in: .whitespacesAndNewlines)
        return title.isEmpty ? localized(.nativeSwiftModerationReportsReviewQueueUntitledPost) : title
    }

    private var displayExcerpt: String {
        let excerpt = item.post.markdownPreview.trimmingCharacters(in: .whitespacesAndNewlines)
        return excerpt.isEmpty ? localized(.nativeSwiftModerationReportsReviewQueueNoPreview) : excerpt
    }

    private var rootContext: String? {
        guard let rootId = item.post.rootId else { return nil }
        return [item.post.rootPostType, item.post.rootSlug, rootId].compactMap { $0 }.joined(separator: " · ")
    }

    private var statusLabel: String {
        localized(item.clearanceStatus == .inReview
            ? .nativeSwiftModerationReportsReviewQueueInReview
            : .nativeSwiftModerationReportsReviewQueueRejected)
    }

    private var spamLabel: String {
        let flag = flaggedLabel(item.post.spamDetectionFlagged == true)
        guard let score = item.post.spamDetectionScore else { return flag }
        return localized(
            .nativeSwiftModerationReportsReviewQueueFlaggedScore,
            parameters: [
                "flag": flag,
                "score": UiMessages.number(score, maximumFractionDigits: 2, locale: nativeUiLocale)
            ]
        )
    }

    private func flaggedLabel(_ flagged: Bool) -> String {
        localized(flagged
            ? .nativeSwiftModerationReportsReviewQueueFlagged
            : .nativeSwiftModerationReportsReviewQueueNotFlagged)
    }

    private func localized(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        UiMessages.string(key, parameters: parameters, locale: nativeUiLocale)
    }
}
