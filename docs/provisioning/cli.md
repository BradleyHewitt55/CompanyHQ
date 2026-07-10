# Provisioning CLI

> **Document status:** Confirmed Target V1 command contract; password transport is a documented risk.

Direct provisioning uses:

```text
companyhq {command} -c CompanyName -s {server_ip} -u {owner_username} -p {owner_password}
```

Saved configuration uses `-cf`:

```text
companyhq {command} -c CompanyName -cf {config_name} -u {owner_username} -p {owner_password}
```

`-c` is the company name; `-s` is the target server route/address; `-cf` selects saved remote connection metadata; `-u` and `-p` are the initial CompanyHQ owner credentials, not SSH credentials. The command obtains other SSH connection settings interactively or from the named local configuration. `{command}` includes install, inspect, upgrade, backup, restore, health, and uninstall-plan operations; actual destructive uninstall requires a separately confirmed execution command.

**Risk:** A password passed with `-p` can appear in shell history and process listings. V1 must mask echoed input/output, scrub argument storage immediately, exclude it from audit/telemetry, never echo it in errors, never forward it to unrelated child arguments/environment, transmit it only through a protected channel, hash it before account storage, and warn before execution. The safer interactive secret prompt should be offered when `-p` is omitted and should become the recommended form without breaking the confirmed syntax.

Saved configurations contain host, port, SSH user, verified host fingerprint, identity reference, company key, and safe defaults. They never store owner passwords, private keys, recovery codes, or provider tokens.
