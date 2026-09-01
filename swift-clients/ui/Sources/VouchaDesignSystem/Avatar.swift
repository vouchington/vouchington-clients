import SwiftUI

public struct Avatar: View {
    public let imageURL: String?
    public let username: String
    public let size: CGFloat

    public init(imageURL: String?, username: String, size: CGFloat = 36) {
        self.imageURL = imageURL
        self.username = username
        self.size = size
    }

    private var initials: String {
        let components = username.split(separator: " ")
        if components.count >= 2,
           let first = components.first?.first,
           let second = components.dropFirst().first?.first {
            return "\(first)\(second)".uppercased()
        }
        return String(username.prefix(2)).uppercased()
    }

    public var body: some View {
        Group {
            if let urlString = imageURL, !urlString.isEmpty {
                AsyncImage(url: URL(string: urlString)) { phase in
                    switch phase {
                    case let .success(image):
                        image
                            .resizable()
                            .scaledToFill()
                    default:
                        initialsView
                    }
                }
            } else {
                initialsView
            }
        }
        .frame(width: size, height: size)
        .clipShape(Circle())
    }

    private var initialsView: some View {
        Circle()
            .fill(Colors.primary.opacity(0.2))
            .overlay {
                Text(initials)
                    .font(.system(size: size * 0.38, weight: .semibold))
                    .foregroundStyle(Colors.primary)
            }
    }
}
