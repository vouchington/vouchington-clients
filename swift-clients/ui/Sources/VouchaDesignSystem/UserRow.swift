import SwiftUI
import VouchaModels

/// A single-row representation of a public user: avatar, username, and bio snippet.
public struct UserRow: View {
    public let user: PublicUser
    public let avatarURL: String?

    public init(user: PublicUser, avatarURL: String?) {
        self.user = user
        self.avatarURL = avatarURL
    }

    public var body: some View {
        HStack(alignment: .top, spacing: Spacing.sm) {
            Avatar(imageURL: avatarURL, username: user.username ?? "")
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(user.username ?? "")
                    .font(Typography.subheadline)
                    .fontWeight(.semibold)
                    .foregroundStyle(.primary)
                if let markdown = user.markdown, !markdown.isEmpty {
                    NativeHtmlContent(
                        html: nil,
                        fallback: markdown,
                        lineLimit: 2,
                        font: Typography.body,
                        foregroundStyle: .secondary
                    )
                }
            }
            Spacer()
        }
        .padding(.vertical, Spacing.xs)
    }
}
