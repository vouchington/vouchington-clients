import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativePostComposeSurface {
    func imageSection(viewModel: NativePostComposeViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            imagePickerControls(viewModel: viewModel)

            if !viewModel.images.isEmpty {
                imageList(viewModel: viewModel)
            }
        }
    }

    private func imagePickerControls(viewModel: NativePostComposeViewModel) -> some View {
        HStack(spacing: Spacing.sm) {
            Button {
                showingImageImporter = true
            } label: {
                Label(
                    UiMessages.string(
                        viewModel.canAddMoreImages
                            ? .nativeSwiftPostComposeAddImages
                            : .nativeSwiftPostComposeImageLimitReached,
                        locale: nativeUiLocale
                    ),
                    systemImage: "photo.on.rectangle"
                )
            }
            .buttonStyle(.bordered)
            .disabled(!viewModel.canAddMoreImages || viewModel.isUploadingImages || viewModel.isLoading)

            if viewModel.isUploadingImages {
                ProgressView()
            }

            if let error = viewModel.imageUploadErrorMessage {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }

    private func imageList(viewModel: NativePostComposeViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftPostComposeImages, locale: nativeUiLocale))
                .font(Typography.subheadline)
                .fontWeight(.semibold)

            ForEach(Array(viewModel.images.enumerated()), id: \.element.id) { index, image in
                imageRow(viewModel: viewModel, image: image, index: index)
            }
        }
    }

    private func imageRow(
        viewModel: NativePostComposeViewModel,
        image: NativePostComposeImageDraft,
        index: Int
    ) -> some View {
        HStack(spacing: Spacing.sm) {
            Text(UiMessages.string(
                .nativeSwiftPostComposeImageLabel,
                parameters: ["index": UiMessages.number(index + 1, locale: nativeUiLocale)],
                locale: nativeUiLocale
            ))
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)
            .frame(width: 72, alignment: .leading)

            TextField(UiMessages.string(.nativeSwiftPostComposeCaption, locale: nativeUiLocale), text: Binding(
                get: { viewModel.images.first(where: { $0.id == image.id })?.caption ?? "" },
                set: { viewModel.updateImageCaption(id: image.id, caption: $0) }
            ))
            .textFieldStyle(.roundedBorder)

            Button {
                viewModel.moveImageUp(id: image.id)
            } label: {
                Image(systemName: "chevron.up")
            }
            .buttonStyle(.borderless)
            .disabled(index == 0)

            Button {
                viewModel.moveImageDown(id: image.id)
            } label: {
                Image(systemName: "chevron.down")
            }
            .buttonStyle(.borderless)
            .disabled(index == viewModel.images.count - 1)

            Button(role: .destructive) {
                viewModel.removeImage(id: image.id)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
        }
    }
}
