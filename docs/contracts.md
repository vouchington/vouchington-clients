# Filaments contract boundary

Native clients do not read arbitrary files from Filaments. `contracts/filaments.json` fixes the
repository, branch, and allowed source paths. CI owns obtaining a normal full Filaments checkout,
then supplies its root explicitly through `VOUCHA_FILAMENTS_CONTRACT_ROOT`; the contract command
validates the required layout and never performs repository acquisition itself.

The contract command verifies the explicit root and every allowlisted source tree is a real
directory with no symlinks. It stages each generated localization copy before replacing the tracked
Swift and .NET directory. If a local sync is interrupted between replacements, restore the tracked
outputs with Git and run the sync again.

`contracts:check` is fail-closed: a missing root or source tree, a symlink, or output mismatch
fails the command. It never fetches or writes. Only `contracts:sync` replaces generated outputs.
