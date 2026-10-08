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

Security fixes land on `main` and ship in the next build of each native client.
Use the latest available client build. We do not maintain separate security
patch branches for older store or downloaded builds; update to the fixed build
when it becomes available.

## Not Vulnerabilities

The synthetic cookie values in
[`SessionCookieJarTests.cs`](dotnet-clients/tests/Voucha.Client.Core.Tests/Auth/SessionCookieJarTests.cs)
and [`AuthCoverageTests.cs`](dotnet-clients/tests/Voucha.Client.Core.Tests/Auth/AuthCoverageTests.cs)
are test inputs, not production credentials. They belong to the portable test
project and are not packaged into the application. The development cookie
bootstrap in
[`SessionCookieJar.Development.cs`](dotnet-clients/src/Voucha.Client.Core/Auth/SessionCookieJar.Development.cs)
is additionally compiled only under `DEBUG`.

These exceptions apply only to those synthetic test inputs. An exposed real
session cookie, API key, signing key, or a way to use a test credential in
production should be reported privately through the channel above.
