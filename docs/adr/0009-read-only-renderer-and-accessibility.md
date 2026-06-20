# Read-only renderer and basic accessibility

The renderer is read-only: it displays, selects, and copies, but never accepts text input. In-place editing (e.g. editing a sent message) is the host's job via a separate ordinary `TextBox`. Accessibility is provided at a basic level: an `AutomationPeer` exposes the whole Document as accessible read-only plain text.

Because the component never edits, the entire text-input stack — caret, IME, composition, text services — is out of scope. This corrects ADR-0005, which over-listed IME as a self-built workstream; a read-only self-drawn surface simply does not need it. The remaining cost from self-drawing is selection (ADR-0008) and accessibility. For accessibility we reuse the plain-text serialization (ADR-0008) and surface it through one AutomationPeer, which has a second payoff: self-drawn content is otherwise invisible to UI-test frameworks and to Quicker's own automation, so the peer is what lets tests assert rendered content. Structured, per-Block accessibility (roles, navigation) is deferred.

## Consequences

- "Read-only" is a deliberate product boundary; adding in-place editing later would reopen caret/IME/text-input and should be a conscious decision, not a drive-by feature.
- The AutomationPeer's text mirrors the plain-text copy output, so the two stay defined together.
