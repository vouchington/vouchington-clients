import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaModels

struct PostAuthorAvatar: View {
    let post: Post

    private var visibleImageURL: String? {
        guard post.deletedAt == nil, !post.isAnonymous,
              let placement = post.createdBy?.profileImagePlacement else {
            return nil
        }
        return AppConfig.shared.imageURL(for: placement, width: 48)
    }

    var body: some View {
        if let visibleImageURL {
            AsyncImageView(
                urlString: visibleImageURL,
                baseURL: AppConfig.shared.imageBaseURL,
                contentMode: .fill
            )
            .frame(width: 24, height: 24)
            .clipShape(Circle())
            .accessibilityLabel(Text(verbatim: post.createdBy?.username ?? ""))
        }
    }
}
