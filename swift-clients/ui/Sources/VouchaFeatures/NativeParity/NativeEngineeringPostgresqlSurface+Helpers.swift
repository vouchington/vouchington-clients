import SwiftUI
import VouchaAPI
import VouchaLocalization

extension NativeEngineeringPostgresqlSurface {
    func confirmMessage(for action: NativeEngineeringPostgresqlPendingAction?) -> UiMessage? {
        switch action {
        case .createPartitions:
            UiMessage(.nativeSwiftEngineeringPostgresqlCreatePartitionsConfirmation)
        case .cleanupPartitions:
            UiMessage(.nativeSwiftEngineeringPostgresqlCleanupPartitionsConfirmation)
        case .none:
            nil
        }
    }

    func confirmButtonTitle(for action: NativeEngineeringPostgresqlPendingAction?) -> UiMessageKey {
        switch action {
        case .createPartitions:
            .nativeSwiftEngineeringPostgresqlCreatePartitions
        case .cleanupPartitions:
            .nativeSwiftEngineeringPostgresqlCleanupPartitions
        case .none:
            .nativeSwiftCommonConfirm
        }
    }

    func title(for type: EngineeringPsqlJobType) -> UiMessage {
        switch type {
        case .runMigrations:
            UiMessage(.nativeSwiftEngineeringPostgresqlRunMigrations)
        case .runViews:
            UiMessage(.nativeSwiftEngineeringPostgresqlRunViews)
        case .runConfigDriven:
            UiMessage(.nativeSwiftEngineeringPostgresqlRunConfigDriven)
        case .createPartitions:
            UiMessage(.nativeSwiftEngineeringPostgresqlCreatePartitions)
        case .cleanupPartitions:
            UiMessage(.nativeSwiftEngineeringPostgresqlCleanupPartitions)
        }
    }

    func detail(for type: EngineeringPsqlJobType) -> UiMessage {
        switch type {
        case .runMigrations:
            UiMessage(.nativeSwiftEngineeringPostgresqlApplyPendingMigrations)
        case .runViews:
            UiMessage(.nativeSwiftEngineeringPostgresqlRebuildViews)
        case .runConfigDriven:
            UiMessage(.nativeSwiftEngineeringPostgresqlRunConfigDrivenDetail)
        case .createPartitions:
            UiMessage(.nativeSwiftEngineeringPostgresqlCreateFuturePartitions)
        case .cleanupPartitions:
            UiMessage(.nativeSwiftEngineeringPostgresqlDropExpiredPartitions)
        }
    }

    func partitionDetail(_ table: EngineeringPartitionTable) -> UiMessage {
        UiMessage(
            .nativeSwiftEngineeringPostgresqlPartitionSummary,
            numberParameters: [
                "partitions": Double(table.partitionCount),
                "bytes": Double(table.totalSizeBytes)
            ]
        )
    }
}
