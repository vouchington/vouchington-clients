import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension CRMContactsSurface {
    func emailHistorySection(viewModel: CRMContactsViewModel) -> some View {
        Group {
            emailMessagesSection(viewModel: viewModel)
            emailComposerSection(viewModel: viewModel)
        }
    }

    private func emailMessagesSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftCrmContactsEmailHistory,
            locale: nativeUiLocale
        ))) {
            ForEach(viewModel.selectedEmails) { message in
                emailMessageRow(message)
            }
            HybridPaginationControl(
                hasMore: viewModel.emailPagination.hasMore,
                isLoading: viewModel.emailPagination.isLoading,
                hasError: viewModel.emailPagination.lastError != nil
            ) {
                await viewModel.loadMoreEmails()
            }
            if let message = viewModel.emailErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }

    private func emailComposerSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftCrmContactsEmailComposer,
            locale: nativeUiLocale
        ))) {
            emailDraftControls(viewModel: viewModel)
            emailContentFields(viewModel: viewModel)
            Button {
                Task { await viewModel.sendEmail() }
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftCrmContactsSendEmail, locale: nativeUiLocale),
                    systemImage: "paperplane"
                )
            }
            .buttonStyle(.borderedProminent)
            .disabled(viewModel.isSendingEmail)
        }
    }

    private func emailDraftControls(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Group {
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsAiPrompt, locale: nativeUiLocale),
                text: $viewModel.emailDraftPrompt,
                axis: .vertical
            )
            .lineLimit(2 ... 4)
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsTone, locale: nativeUiLocale),
                text: $viewModel.emailDraftTone
            )
            .textFieldStyle(.roundedBorder)
            HStack {
                Button {
                    Task { await viewModel.generateEmailDraft() }
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftCrmContactsAiDraft, locale: nativeUiLocale),
                        systemImage: "sparkles"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.isDraftingEmail)

                Picker(
                    UiMessages.string(.nativeSwiftCrmContactsProvider, locale: nativeUiLocale),
                    selection: $viewModel.emailProvider
                ) {
                    ForEach(CrmEmailProvider.allCases, id: \.self) { provider in
                        Text(UiMessages.string(provider.titleText, locale: nativeUiLocale)).tag(provider)
                    }
                }
                .pickerStyle(.menu)
            }
        }
    }

    private func emailContentFields(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Group {
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsSubject, locale: nativeUiLocale),
                text: $viewModel.emailSubject
            )
            .textFieldStyle(.roundedBorder)
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsBodyText, locale: nativeUiLocale),
                text: $viewModel.emailBodyText,
                axis: .vertical
            )
            .lineLimit(4 ... 10)
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsBodyHtml, locale: nativeUiLocale),
                text: $viewModel.emailBodyHtml,
                axis: .vertical
            )
            .lineLimit(4 ... 10)
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsCtaUrl, locale: nativeUiLocale),
                text: $viewModel.emailCtaUrl
            )
            .textFieldStyle(.roundedBorder)
        }
    }

    private func emailMessageRow(_ message: CrmMessage) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack {
                Text(
                    message.subject
                        ?? UiMessages.string(.nativeSwiftCommonMessage, locale: nativeUiLocale)
                )
                Spacer(minLength: 0)
                Text(emailTimestamp(
                    message,
                    locale: nativeUiLocale,
                    timeZone: nativeUiTimeZone
                ))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            }
            NativeHtmlContent(
                html: message.bodyHtml,
                fallback: message.bodyText,
                font: Typography.caption,
                foregroundStyle: Colors.secondaryLabel
            )
        }
    }

    func emailTimestamp(
        _ message: CrmMessage,
        locale: Locale,
        timeZone: TimeZone
    ) -> String {
        UiMessages.date(
            message.sentAt ?? message.receivedAt ?? message.createdAt,
            date: .abbreviated,
            time: .shortened,
            locale: locale,
            timeZone: timeZone
        )
    }
}
