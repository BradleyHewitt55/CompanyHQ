# Hermes adapter

> **Document status:** Target V1 adapter contract informed by the Hermes reference study.

Hermes may provide planning, memory, scheduled intents, and multi-step execution through a CompanyHQ adapter. CompanyHQ remains authoritative for tasks, schedules, capabilities, approvals, mutations, audit, and recovery.

Hermes tools are replaced or wrapped by the installation-local MCP catalog. Its SQLite/profile memory, cron files, local shell/filesystem/network, and provider credentials are not trusted isolation boundaries. Memory is partitioned by installation and profile; task data is retained only by explicit policy.
