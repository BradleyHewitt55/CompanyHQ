# Agent chat

> **Document status:** Target V1 product contract.

Admin chat is a task/run interface with streamed messages, tool activity summaries, approvals, user questions, changed resources/files, costs, plan, connection state, cancellation, and recovery. Conversation messages are durable and versioned; provider-native transcripts remain adapter details.

The server builds context from authorized DTOs, task scope, selected project/worktree, policy, and tool catalog. Repository/webhook/content text is labeled untrusted and cannot alter system policy or tool grants. Large history is summarized with provenance and retained source references.
