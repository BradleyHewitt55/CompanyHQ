# Brain dump to task

> **Document status:** Target V1 product behavior.

Free-form input is treated as untrusted source material. An agent may propose a title, objective, scope, exclusions, acceptance criteria, dependencies, risks, affected resources, approval class, and subtasks. The human reviews the structured proposal before it becomes runnable.

The parser cannot infer permissions, enable tools/providers, select secrets, approve external effects, or silently expand scope. Original input and proposal version remain linked for audit.
