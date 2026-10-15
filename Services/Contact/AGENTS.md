# Contact service

`Services/Contact` is the public contact form behind `Apps/Marketing`. It owns no entity, table,
migration or read path. It references no other service: it asks its own `IContactAddressBook` where
to send and hands the message to its own `IContactMailer`.

## Write-only by design

`POST /api/contact` is anonymous and returns `202` with `ClientContactReceiptDto`. There is no GET,
no list, and no store - a submission becomes one transactional email and nothing else. The generated
reference is the idempotency key for that single send through `IContactMailer`. Do not add a
persisted model or a read endpoint here; either would turn this route into a way to enumerate who has
written in.

`202` means the message is stored in the Email outbox, not delivered. Provider failures are retried
by the Email worker and never reach this route. A `502` means the mailer returned a failure instead
of queuing, or no addresses are configured.

## Recipient routing

Support and privacy topics go to `ContactAddresses.SupportEmail`. General, partnership, and press
topics go to `ContactAddresses.InfoEmail`. That routing rule is Contact's; where the two addresses
come from is the bridge's. The host's bridge reads them from the Company profile — do not reintroduce
a static or environment-only inbox.

## Untrusted input

Everything in `ContactRequest` is anonymous public input. Bounds are name 2 to 120, message 20 to
4000, email at most 254 and well-formed. The sender's email is never an identity - it is only echoed
into the message staff receive. The Email template renderer HTML-encodes every field before it
reaches a mailbox that renders HTML; keep it that way.

`ContactRateLimit` (`contact-submit`, five per 15 minutes per remote address) is a spam brake, not an
authorization control. The policy is registered by `AddContact`, not by the host.

## Contract

`ContactTopic` carries `JsonStringEnumConverter<ContactTopic>`. Without it the enum generates into
the marketing site's TypeScript as `number`.

Both ports are required. `Apps/Api/Integrations/ContactIntegration.cs` fills them from Company and
Email; without those lines startup fails naming the missing port. The database must contain the
singleton company profile before a contact message can be queued.

## Runtime catalog constraints

Contact declares Company and Email dependencies in the opt-in catalog. Disabled admission is absent; missing delivery configuration never fabricates acceptance or invokes a live provider from the request.
