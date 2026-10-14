import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaModels

struct PostAuthorAvatar: View {
    let post: Post
    let imageConfig: AppConfig

    private var visibleImageURL: String? {
        guard post.deletedAt == nil, !post.isAnonymous,
              let placement = post.createdBy?.profileImagePlacement else {
            return nil
        }
        return imageConfig.imageURL(for: placement, width: 48)
    }

    var body: some View {
        if let visibleImageURL {
            AsyncImageView(
                urlString: visibleImageURL,
                baseURL: imageConfig.imageBaseURL,
                contentMode: .fill
            )
            .frame(width: 24, height: 24)
            .clipShape(Circle())
            .accessibilityHidden(true)
        }
    }
}
