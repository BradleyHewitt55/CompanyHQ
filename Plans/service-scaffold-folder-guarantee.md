# Service scaffold folder guarantee

## Goal

Guarantee every user-created service includes the full convention tree, including `Services/`.

## Scope

- Preserve `Services/` creation in `ServiceScaffolder.Generate`.
- Preserve `Services/` folders in company blueprint trees.

## Task-specific tests

- Generate a service and assert all six convention folders exist, including `Services/`.
- Assert blueprint trees retain their `Services/` directories.
