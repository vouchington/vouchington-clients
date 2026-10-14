import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaModels

struct CommunityPlacementImages: View {
    let community: Community?
    let imageConfig: AppConfig

    var profileURL: String? {
        imageConfig.imageURL(for: community?.profileImagePlacement, width: 96)
    }

    var bannerURL: String? {
        imageConfig.imageURL(for: community?.bannerImagePlacement, width: 960)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if let bannerURL, let url = URL(string: bannerURL) {
                AsyncImageView(
                    urlString: url.absoluteString,
                    baseURL: imageConfig.imageBaseURL,
                    contentMode: .fill
                )
                .frame(height: 144)
                .clipped()
                .accessibilityHidden(true)
                .accessibilityIdentifier("community-banner-image")
            }
            HStack(spacing: Spacing.sm) {
                CommunityPlacementAvatar(url: profileURL, imageBaseURL: imageConfig.imageBaseURL)
                Spacer(minLength: 0)
            }
        }
    }
}

struct CommunityPlacementAvatar: View {
    let url: String?
    let imageBaseURL: URL

    init(url: String?, imageBaseURL: URL = AppConfig.shared.imageBaseURL) {
        self.url = url
        self.imageBaseURL = imageBaseURL
    }

    init(placement: ImagePlacement?, imageConfig: AppConfig = .shared) {
        url = imageConfig.imageURL(for: placement, width: 96)
        imageBaseURL = imageConfig.imageBaseURL
    }

    var body: some View {
        Group {
            if let url, let parsedURL = URL(string: url) {
                AsyncImageView(
                    urlString: parsedURL.absoluteString,
                    baseURL: imageBaseURL,
                    contentMode: .fill
                )
            } else {
                fallback
            }
        }
        .frame(width: 56, height: 56)
        .clipShape(Circle())
        .accessibilityHidden(true)
        .accessibilityIdentifier("community-profile-image")
    }

    private var fallback: some View {
        Image(systemName: "person.3.fill")
            .resizable()
            .scaledToFit()
            .padding(12)
            .foregroundStyle(.secondary)
            .background(Color.secondary.opacity(0.12))
    }
}
