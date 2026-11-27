---
title: Login
---

## Sign in with an Insights account

Run `terrabuild insights login` and approve the code in your browser. Insights
walks new accounts through artifact identity setup. Save your personal Recovery
Kit in a password manager, download it, or print it, then confirm that it is saved.
Your recovery key is generated in your browser and is never sent to Insights.

If the browser is locked, enter the recovery key from your kit. Approving the
CLI request transfers your account bundle encrypted to that request's temporary
key. Terrabuild stores the identity in macOS Keychain, Windows Credential Manager,
or Linux Secret Service. On Linux, install `secret-tool` and unlock your desktop
keyring. Repeat the same sign-in flow on another machine to use the same identity.

Use **Account recovery** in Insights to create a new Recovery Kit while your
identity is unlocked. If every copy and your kit are lost, replace the identity
there. A workspace Owner or Admin who can still decrypt its root can grant your
replacement access to existing artifacts without rotating the workspace.

Use `terrabuild insights logout` to revoke your current CLI session.

## Sign in to a CI job

A workspace Owner or Admin creates a token on the Insights Security page before the job
can use it. Follow the [Insights setup guide](../getting-started/insights.md) to
issue the token and grant it access. Configure the resulting token as the job’s
`TERRABUILD_TOKEN` secret.

```bash
printf '%s\n' "$TERRABUILD_TOKEN" | terrabuild login --workspace "$WORKSPACE_ID"
```

Terrabuild uses the runner’s private credential directory across job steps.
Set `TERRABUILD_CI_CREDENTIAL_DIR` to a private job directory when your runner
does not supply one automatically. Run `terrabuild logout --space "$WORKSPACE_ID"`
during teardown.
