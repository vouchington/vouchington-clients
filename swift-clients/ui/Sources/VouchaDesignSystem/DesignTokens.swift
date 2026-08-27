import SwiftUI

public enum Colors {
    public static let primary = Color.accentColor
    public static let secondaryLabel = Color.secondary
    #if os(macOS)
        public static let background = Color(nsColor: .windowBackgroundColor)
        public static let separator = Color(nsColor: .separatorColor)
    #else
        public static let background = Color(uiColor: .systemBackground)
        public static let separator = Color(uiColor: .separator)
    #endif
    public static let positiveVote = Color.green
    public static let negativeVote = Color.red
}

public enum Spacing {
    public static let xs: CGFloat = 4
    public static let sm: CGFloat = 8
    public static let md: CGFloat = 12
    public static let lg: CGFloat = 16
    public static let xl: CGFloat = 24
    public static let xxl: CGFloat = 32
}

public enum Typography {
    public static let headline = Font.headline
    public static let subheadline = Font.subheadline
    public static let body = Font.body
    public static let caption = Font.caption
    public static let caption2 = Font.caption2
    public static let largeTitle = Font.largeTitle
}
