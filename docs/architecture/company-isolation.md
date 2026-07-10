# Company isolation

> **Document status:** Confirmed Target V1 security architecture.

The isolation unit is an installation, not a row-level tenant in a shared SaaS database. Each installation belongs to exactly one company and owns independent compute, database credentials, Redis prefix, object-storage prefix or bucket, encryption keys, filesystem root, queues, agents, MCP server, audit stream, and backups.

## Context construction

`CompanyContext` contains `installationId`, immutable `companyKey`, environment, configuration revision, and correlation metadata. It is loaded from signed server configuration, not accepted from a browser header. Request authentication adds `ActorContext`; jobs and agent runs carry signed context envelopes whose installation must match the consuming worker.

Repositories are created only through a context-bound unit of work. They never accept an arbitrary company ID and cannot issue unscoped queries. Tables still carry `installation_id` as defense in depth, for restore validation, auditing, and accidental cross-database import detection. PostgreSQL row-level security may supplement this rule but is not the primary isolation boundary.

## Propagation

| Surface | Required binding |
| --- | --- |
| HTTP request | Server-resolved installation + authenticated session/API principal |
| Queue job | Installation-prefixed queue + signed context envelope + schema version |
| Agent run/tool call | Installation, delegator, agent/profile, task, run, capability, expiry |
| File/object | Installation storage root, resource owner, classification, checksum |
| Cache entry | `companyhq:{installation}:{environment}:{domain}:{key}` |
| Event/audit | Installation, actor, correlation, causation, resource, timestamp |
| Worktree | Canonical path below the installation's `worktrees/` and task ownership |

Every boundary rejects a mismatched or missing installation. No “super-admin across companies” exists in V1. Server operators use infrastructure access outside the product and that activity is not treated as an application role.

## Required tests

Run two complete installations with distinct databases, Redis prefixes, storage roots, keys, domains, users, projects, and agents. Prove that forged headers, copied tokens, job replay, cache-key substitution, object-path traversal, worktree symlinks, backup restore, provider callbacks, MCP calls, and logs cannot cross the boundary. See [cross-company tests](../security/cross-company-tests.md).
