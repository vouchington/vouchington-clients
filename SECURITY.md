# Security Policy

## Reporting a Vulnerability

If you believe you've found a security vulnerability in one of Voucha's native
clients, please report it privately through GitHub's built-in reporting flow
rather than filing a public issue:

1. Go to the [Security tab](https://github.com/vouchington/vouchington-clients/security)
   of this repository.
2. Click **Report a vulnerability**.
3. Fill in as much detail as you can: affected platform (macOS, iOS, Android,
   or Windows), reproduction steps, and potential impact.

**Please do not open a public issue for a suspected vulnerability.** Private
reporting lets us investigate and ship a fix before the details are public.

We don't currently run a paid bug bounty program. You can expect an
acknowledgement within a few business days, and we'll keep you updated as we
investigate and work toward a fix.

## Supported Versions

This repository doesn't publish versioned packages to any registry: there's
no Swift Package Registry, CocoaPods, NuGet, or npm release, and no tagged
releases exist. What's here is the native application source for Voucha's
macOS, iOS, Android, and Windows clients, built directly from `main`. There's
no older supported version to patch separately — security fixes land on
`main` and ship in the next build of each client.
