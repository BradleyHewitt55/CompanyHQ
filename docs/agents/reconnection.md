# Agent reconnection and resume

> **Document status:** Target V1 operational contract.

One connection supervisor owns retry policy per provider instance. It uses exponential backoff with jitter, health/rate-limit classification, and circuit state; competing layers may not create retry storms.

Transport reconnection and task recovery are separate. After reconnect, the adapter validates provider instance/version, reconciles sessions and pending approvals, then resumes only compatible active runs. Admin chat shows cached durable state while disconnected and labels stale/live data.
