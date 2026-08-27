# Filaments contract boundary

Native clients do not read arbitrary files from Filaments. `contracts/filaments.json` fixes the
repository, branch, and allowed source paths. CI owns obtaining a normal full Filaments checkout,
then supplies its root explicitly through `VOUCHA_FILAMENTS_CONTRACT_ROOT`; the contract command
validates the required layout and never performs repository acquisition itself.

The contract command verifies the explicit root and every allowlisted source tree is a real
directory with no symlinks. It stages each generated localization copy, preserves the previous
directory as a sibling backup during replacement, and automatically recovers that backup if a sync
is interrupted before publication. Run the sync again after an interruption.

`contracts:check` is fail-closed: a missing root or source tree, a symlink, or output mismatch
fails the command. It never fetches or writes. Only `contracts:sync` replaces generated outputs.
