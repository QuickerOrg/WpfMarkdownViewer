# Block renderers organized as a registry, extension surface closed for now

Block rendering is dispatched through an internal registry keyed by block type, behind an `IBlockRenderer`-style contract that all built-in renderers (paragraph, heading, code, list, quote, table, image, math) use. The contract is not made public in the first phase: hosts cannot register custom block renderers yet.

The registry shape leaves a clean seam for opening the surface later, but we deliberately do not publish it now. The hard part of an extension surface is not "register a renderer" — it is how a custom Block participates in the cross-cutting contracts: cross-block selection, three-format copy serialization, height-cache virtualization, and the accessibility AutomationPeer. Those contracts are not yet proven even for the built-in blocks, and a published extension API would lock them in prematurely (per ADR-0001/0003, technology and integration contracts are expensive to change once consumers depend on them). We first make the built-ins correct against all of these through the same mechanism, then consider opening it.

## Consequences

- Adding a new block type in the first phase means adding a built-in renderer, not a host plugin.
- "Closed for now" is a deliberate boundary; opening it is an additive, conscious decision once the cross-cutting contracts have stabilized.
