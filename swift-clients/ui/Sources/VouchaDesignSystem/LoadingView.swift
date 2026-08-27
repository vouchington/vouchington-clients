import SwiftUI

public struct LoadingView: View {
    public let label: String?

    public init(label: String? = nil) {
        self.label = label
    }

    public var body: some View {
        VStack(spacing: Spacing.sm) {
            ProgressView()
                .controlSize(.large)
            if let label {
                Text(label)
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(Spacing.xl)
    }
}
