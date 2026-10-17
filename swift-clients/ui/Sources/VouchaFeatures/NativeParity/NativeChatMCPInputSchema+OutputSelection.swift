import VouchaModels

extension NativeChatMCPInputSchema {
    /// Only the contract's explicit root anyOf index enters this path. Recursive schema
    /// visits never populate or reuse the selected root branch's acceptance.
    static func checkSelectedRootOutput(
        _ value: DecodedJSONValue, root: DecodedJSONValue, index: Int, remainingWork: inout Int
    ) throws -> Bool {
        guard case let .object(fields) = root, case let .array(branches)? = fields["anyOf"],
              branches.indices.contains(index) else { throw Failure.unsupportedSchema }
        guard try check(
            value,
            schema: root,
            root: root,
            depth: 0,
            remainingWork: &remainingWork,
            output: true,
            skipRootAnyOf: true
        ) else { return false }
        var anyAccepted = false
        var selectedAccepted = false
        for (branchIndex, branch) in branches.enumerated() {
            let accepted = try check(
                value,
                schema: branch,
                root: root,
                depth: 1,
                remainingWork: &remainingWork,
                output: true
            )
            anyAccepted = anyAccepted || accepted
            if branchIndex == index { selectedAccepted = accepted }
        }
        return anyAccepted && selectedAccepted
    }
}
