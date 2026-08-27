import VouchaModels

extension CRMContactsViewModel {
    func formatImportValidationErrors(_ validation: CrmImportValidation) -> String {
        validation.rows
            .flatMap { row in
                row.errors.map { "Row \(row.rowIndex + 2): \($0)" }
            }
            .joined(separator: "\n")
    }
}
