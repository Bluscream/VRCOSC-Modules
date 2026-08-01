# Code map

A generated inventory of everything declared in `VRCOSC.Modules/` — what exists and where,
so you can find a declaration without grepping. Not API documentation: there are no
descriptions or usage notes, just declarations with clickable `file:line` links.

| File | Contents |
|---|---|
| [classes.md](classes.md) | classes, structs, interfaces, records, enums |
| [methods.md](methods.md) | methods and constructors |
| [properties.md](properties.md) | properties, including auto and expression-bodied |
| [fields.md](fields.md) | fields, including `const` and `readonly` |
| [events.md](events.md) | VRCOSC ChatBox events and C# `event` declarations |

`events.md` covers two unrelated things that share the name: user-facing ChatBox events
(`CreateEvent` / `TriggerEvent`, bindable in the ChatBox editor) and internal C# `event`
declarations. It also cross-references every fire site, so an event that is declared but
never raised shows as **never**.

Each file groups by module, then by source file, with a per-module count table at the top.
Members list their declaring type in an Owner column.

## Regenerating

```bash
python3 tools/gen-docs.py
```

**Do not edit these by hand** — edit `gen-docs.py` and regenerate. Run it after adding or
renaming types so the map does not drift.

## Accuracy

The generator is a hand-rolled C# declaration scanner, not Roslyn — Roslyn would mean a
build step for what is essentially a listing. It strips comments and string literals so
their braces cannot confuse it, then tracks brace depth so that only declarations sitting
directly in a type body are counted (without that, every local variable inside a method
was being reported as a field).

It is deliberately conservative: anything it cannot confidently classify is skipped rather
than guessed at, so treat the counts as a close lower bound. Spot-checked against several
files at the time of writing with exact agreement, but constructs it is known not to model
include explicit interface implementations, multi-line signatures split before the
parameter list, and multiple declarators in one field statement (`int a, b;` counts once).

Fields outnumber everything else by a wide margin, which is expected rather than a parsing
error: VRCOSC node pins (`ValueInput`, `FlowOutput`, …) are declared as fields, and the
node classes are numerous.
