# Filaments contract boundary

Native clients do not read arbitrary files from Filaments. `contracts/filaments.json` fixes the
repository, branch, and allowed source-to-destination mappings. CI checks out Filaments normally,
then supplies that complete checkout explicitly through `VOUCHA_FILAMENTS_CONTRACT_ROOT`.

The contract command verifies the explicit root and every allowlisted source tree is a real
directory with no symlinks. It then replaces the generated Swift and .NET localization trees from
the checked-out native-localization source. If a local sync is interrupted, restore the tracked
outputs with Git and run the sync again.

`contracts:check` is fail-closed: a missing root or source tree, a symlink, or output mismatch
fails the command. It never fetches or writes. Only `contracts:sync` replaces generated outputs.
