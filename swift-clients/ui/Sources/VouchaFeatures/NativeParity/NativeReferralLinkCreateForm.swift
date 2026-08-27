import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeReferralLinkCreateForm: View {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: NativeReferralLinksManagementViewModel
    @Binding
    var searchQuery: String
    @Binding
    var selectedProgram: NativeReferralProgramChoice?
    @Binding
    var referralURL: String
    @Binding
    var label: String
    @State
    private var validationInfo: ReferralProgramValidationInfo?
    @State
    private var validationInfoProgramId: String?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            programSearch
            programPicker
            TextField(referralPlaceholder, text: $referralURL)
            helpText
            TextField(UiMessages.string(.nativeSwiftReferralLinksManagementLabel, locale: nativeUiLocale), text: $label)
            createButton
        }
    }

    private var programSearch: some View {
        HStack {
            TextField(
                UiMessages.string(.nativeSwiftReferralLinksManagementReferralProgram, locale: nativeUiLocale),
                text: $searchQuery
            )
            Button(UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale)) {
                selectedProgram = nil
                validationInfo = nil
                Task { await viewModel.searchReferralPrograms(query: searchQuery) }
            }
            .disabled(searchQuery.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        }
    }

    @ViewBuilder
    private var programPicker: some View {
        if !viewModel.programs.isEmpty {
            Picker(
                UiMessages.string(.nativeSwiftReferralLinksManagementProgram, locale: nativeUiLocale),
                selection: $selectedProgram
            ) {
                Text(UiMessages.string(.nativeSwiftCommonChoose, locale: nativeUiLocale))
                    .tag(NativeReferralProgramChoice?.none)
                ForEach(viewModel.programs) { program in
                    Text(program.name).tag(Optional(program))
                }
            }
            .onChange(of: selectedProgram) { _, program in
                loadValidationInfo(for: program)
            }
        }
    }

    private var referralPlaceholder: String {
        validationInfo?.exampleUrls.first
            ?? UiMessages.string(.nativeSwiftReferralLinksManagementReferralUrl, locale: nativeUiLocale)
    }

    @ViewBuilder
    private var helpText: some View {
        if let helpText = validationInfo?.userHelpText {
            Text(helpText)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }

    private var createButton: some View {
        Button(UiMessages.string(.nativeSwiftReferralLinksManagementAddReferralLink, locale: nativeUiLocale)) {
            guard let selectedProgram else { return }
            Task { await create(program: selectedProgram) }
        }
        .disabled(selectedProgram == nil || referralURL.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
    }

    private func loadValidationInfo(for program: NativeReferralProgramChoice?) {
        validationInfo = nil
        validationInfoProgramId = program?.id
        guard let program else { return }
        Task {
            let info = await viewModel.fetchValidationInfo(referralProgramId: program.id)
            guard validationInfoProgramId == program.id else { return }
            validationInfo = info
        }
    }

    private func create(program: NativeReferralProgramChoice) async {
        let created = await viewModel.create(
            referralProgramId: program.id,
            url: referralURL,
            label: label.isEmpty ? nil : label
        )
        guard created else { return }
        referralURL = ""
        label = ""
        selectedProgram = nil
        validationInfo = nil
        validationInfoProgramId = nil
    }
}
