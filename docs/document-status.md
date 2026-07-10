# Documentation status

> **Document status:** Confirmed documentation policy.

Every CompanyHQ document and substantive section must distinguish these labels:

| Label | Meaning |
| --- | --- |
| Confirmed | A product decision explicitly supplied or accepted by the owner. |
| Verified implementation | Observed in CompanyHQ source and tests. None exists yet. |
| Target V1 | Required behavior for the first production architecture. |
| Recommendation | Proposed design awaiting implementation review. |
| Reference finding | Pattern observed in a read-only reference repository, not a CompanyHQ feature. |
| Risk | Security, operational, licensing, or delivery concern. |
| Assumption | Working premise that needs validation. |
| Open question | Decision still required. |
| V2 | Deliberately deferred beyond the first production release. |

Statements without a local qualifier inherit the document-level status. Reference studies may name reference projects; product contracts should describe adopted patterns in CompanyHQ terms and remain independently implementable.

## Current truth

CompanyHQ is documentation-only. A feature is not implemented merely because a document specifies it. When code is added, documents must identify the exact verified packages, entry points, migrations, and tests.
