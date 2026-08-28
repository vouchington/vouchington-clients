# Filaments contract boundary

Native clients do not read arbitrary files from Filaments. `contracts/filaments.json` fixes the
repository, branch, and allowed source paths. CI owns obtaining a normal full Filaments checkout,
then supplies its root explicitly through `VOUCHA_FILAMENTS_CONTRACT_ROOT`; the contract command
validates the required layout and never performs repository acquisition itself.

The localization sources are Filaments' already-generated Swift and .NET resource trees. Reusing
those exact trees avoids publishing or maintaining a duplicate contract bundle. The contract
command verifies the explicit root and every allowlisted source tree is a real directory with no
symlinks. It stages each generated localization copy, preserves the previous directory as a sibling
backup during replacement, and automatically recovers that backup if a sync is interrupted before
publication. Run the sync again after an interruption.

Swift test targets share the same root, required-path, and fixture-path validation through the
repository-local `swift-clients/test-support` package.

`contracts:check` is fail-closed: a missing root or source tree, a symlink, or output mismatch
fails the command. In CI this is the byte-for-byte assertion that the clients remain synchronized
with Filaments. The privileged parity workflow executes only assertion code from the trusted base
revision; the pull request checkout is treated as data and is never executed on the self-hosted
runner. The candidate destination must be an absolute checkout path, and every path component is
verified as a real directory rather than a symlink. It never fetches or writes. Only
`contracts:sync` replaces generated outputs in this repository.
