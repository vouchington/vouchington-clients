import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeReviewQueueMediaGroup: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let post: AdminReviewQueuePost
    @Bindable
    var viewModel: NativeReviewQueueViewModel

    var body: some View {
        let media = post.mediaReveal
        if !media.images.isEmpty {
            if media.requiresReveal, !viewModel.isMediaRevealed(postId: post.id) {
                revealGate
            } else {
                images(media.images)
            }
        }
    }

    private var revealGate: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(localized(.nativeSwiftModerationReportsReviewQueueSensitiveMediaHidden))
            if viewModel.exposureIsStale {
                Text(localized(.nativeSwiftModerationReportsReviewQueueExposureStale))
                    .foregroundStyle(.secondary)
                refetchButton(.nativeSwiftModerationReportsReviewQueueCheckExposure)
            } else if viewModel.exposureState?.inCooldown == true {
                Text(localized(.nativeSwiftModerationReportsReviewQueueCooldownPrompt))
                    .foregroundStyle(.secondary)
                refetchButton(.nativeSwiftModerationReportsReviewQueueCheckAgain)
            }
            Button(localized(.nativeSwiftModerationReportsReviewQueueReveal)) {
                Task { await viewModel.revealMedia(postId: post.id) }
            }
            .disabled(!viewModel.canRevealMedia(postId: post.id))
        }
    }

    private func images(_ values: [AdminReviewQueueImage]) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(values.sorted(by: { $0.orderIndex < $1.orderIndex }), id: \.placementId) { image in
                AsyncImageView(
                    urlString: AppConfig.shared.imageURL(
                        forPlacementId: image.placementId,
                        revision: image.placementRevision,
                        imageId: image.imageId,
                        width: 960
                    ),
                    baseURL: AppConfig.shared.imageBaseURL,
                    contentMode: .fit
                )
                .frame(maxWidth: .infinity, minHeight: 180, maxHeight: 360)
                .clipShape(RoundedRectangle(cornerRadius: 8))
                if !image.caption.isEmpty {
                    Text(verbatim: UiMessages.string(.userContent(image.caption), locale: nativeUiLocale))
                        .foregroundStyle(.secondary)
                }
            }
        }
    }

    private func refetchButton(_ title: UiMessageKey) -> some View {
        Button(localized(title)) {
            Task { await viewModel.refetchExposure() }
        }
        .disabled(!viewModel.isAuthorized)
    }

    private func localized(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: nativeUiLocale)
    }
}
