# Single-document core with an optional conversation shell

The core component renders exactly one Document (one Markdown stream). A separate, thin, optional Conversation Shell composes many Documents into a transcript and owns message-level virtualization and autoscroll.

The shared-infrastructure mandate includes non-chat consumers — plugin output and action docs — that are not conversations. Binding the renderer to a conversation model would exclude them and bloat the core. So the core's only job is "turn a token stream into Blocks," it owns block-level virtualization within one long Document, and the host (or the optional shell) owns message-level concerns. The cost is two public surfaces to design and maintain instead of one.
