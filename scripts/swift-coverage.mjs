import { execFileSync } from "node:child_process";

const base = process.env.BASE_SHA;
if (!/^[0-9a-f]{40}$/.test(base ?? "")) {
  throw new Error("BASE_SHA must be the 40-character pull-request base SHA");
}
try {
  execFileSync("git", ["cat-file", "-e", `${base}^{commit}`], { stdio: "ignore" });
} catch {
  throw new Error("BASE_SHA must identify a commit in the repository");
}

execFileSync(
  "pnpm",
  [
    "exec",
    "coverage-check",
    "check",
    "--rules",
    ".coverage-rules.yml",
    "--artifacts",
    "coverage",
    "--require-artifact",
    "core/lcov.info",
    "--require-artifact",
    "ui/lcov.info",
    "--base",
    base,
    "--head",
    "HEAD",
    "--aggregate-artifacts",
    "--fail-on-empty",
    "--annotate-source",
  ],
  { stdio: "inherit" },
);
