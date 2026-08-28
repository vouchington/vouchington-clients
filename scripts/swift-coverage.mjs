import { execFileSync } from "node:child_process";

const base = process.env.BASE_SHA;
if (!/^[0-9a-f]{40}$/.test(base ?? "")) {
  throw new Error("BASE_SHA must be the 40-character pull-request base SHA");
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
