import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension StaffSupportSurface {
    func staffContactDetail(
        _ viewModel: StaffSupportViewModel,
        contact: SupportContact,
        onNavigateToTargetPath: @escaping (String) -> Void
    ) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(contact.emailAddress).font(Typography.headline)
                if !contact.name.isEmpty {
                    Text(contact.name)
                }
                if !contact.notes.isEmpty {
                    Text(contact.notes)
                }
                ForEach(viewModel.contactThreads) { thread in
                    Button {
                        onNavigateToTargetPath("/support/threads/\(thread.id)")
                    } label: {
                        VStack(alignment: .leading) {
                            Text(thread.subject)
                            Text(UiMessages.string(supportThreadStatusText(thread.status), locale: nativeUiLocale))
                                .font(Typography.caption)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                    }
                    .buttonStyle(.plain)
                }
                if viewModel.contactThreadPageInfo?.hasNextPage == true,
                   let cursor = viewModel.contactThreadPageInfo?.endCursor {
                    Button {
                        Task { await viewModel.selectContact(contact.id, after: cursor) }
                    } label: { Text(UiMessages.string(.nativeSwiftCommonLoadMore, locale: nativeUiLocale)) }
                }
            }
        }
    }
}
