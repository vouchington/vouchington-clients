import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

extension NativeEngineeringPostgresqlSurface {
    func migrationSection(_ viewModel: NativeEngineeringPostgresqlViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            sectionTitle(UiMessages.string(.nativeSwiftEngineeringPostgresqlMigrations, locale: nativeUiLocale))
            if let migrations = viewModel.migrations {
                Text(UiMessages.string(
                    .nativeSwiftEngineeringPostgresqlMigrationSummary,
                    parameters: [
                        "applied": UiMessages.number(migrations.applied.count, locale: nativeUiLocale),
                        "pending": UiMessages.number(migrations.pending.count, locale: nativeUiLocale)
                    ],
                    locale: nativeUiLocale
                ))
                .font(.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
                if !migrations.pending.isEmpty {
                    pendingMigrations(migrations.pending)
                }
            }
        }
    }

    func partitionSection(_ viewModel: NativeEngineeringPostgresqlViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            sectionTitle(UiMessages.string(.nativeSwiftEngineeringPostgresqlPartitions, locale: nativeUiLocale))
            if let partitions = viewModel.partitions {
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(partitions.tables) { table in
                        NativeSurfaceRow(
                            row: .init(
                                icon: "cylinder.split.1x2",
                                title: .verbatim(table.name),
                                detail: .app(partitionDetail(table))
                            )
                        )
                    }
                }
            }
        }
    }

    func jobSection(_ viewModel: NativeEngineeringPostgresqlViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            sectionTitle(UiMessages.string(.nativeSwiftEngineeringPostgresqlJobs, locale: nativeUiLocale))
            LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(EngineeringPsqlJobType.allCases, id: \.self) { type in
                    jobRow(type, viewModel: viewModel)
                }
            }
        }
    }

    private func pendingMigrations(_ migrations: [String]) -> some View {
        LazyVStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(migrations, id: \.self) { migration in
                Text(verbatim: UiMessages.string(.verbatim(migration), locale: nativeUiLocale))
                    .font(.caption.monospaced())
            }
        }
    }

    private func jobRow(
        _ type: EngineeringPsqlJobType,
        viewModel: NativeEngineeringPostgresqlViewModel
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            NativeSurfaceRow(row: .init(icon: "hammer", title: title(for: type), detail: detail(for: type)))
            HStack {
                Spacer()
                Button {
                    runOrConfirm(type, viewModel: viewModel)
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftEngineeringPostgresqlRun, locale: nativeUiLocale),
                        systemImage: "play.circle"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.isActionLoading("job|\(type.rawValue)"))
            }
        }
    }

    private func runOrConfirm(
        _ type: EngineeringPsqlJobType,
        viewModel: NativeEngineeringPostgresqlViewModel
    ) {
        if type == .createPartitions || type == .cleanupPartitions {
            viewModel.pendingAction = type == .createPartitions ? .createPartitions : .cleanupPartitions
        } else {
            Task { await viewModel.runJob(type) }
        }
    }
}
