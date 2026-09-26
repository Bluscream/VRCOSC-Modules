#!/usr/bin/env python3
"""
Generate docs/{classes,methods,properties,fields}.md from the C# sources.

Regenerate with:  python3 tools/gen-docs.py

These files are generated - edit this script, not the markdown. They are a map of the
codebase (what exists and where), not API documentation: each entry links to file:line so
it is clickable in an editor.

Parsing is a hand-rolled C# declaration scanner rather than Roslyn, because Roslyn would
mean adding a build step for what is essentially a listing. It strips comments and string
literals first so that braces and keywords inside them cannot confuse the scanner, then
tracks brace depth to know which type owns each member. It is deliberately conservative:
anything it cannot confidently classify is skipped rather than guessed at, so treat the
counts as a close lower bound.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent  # tools/ -> repo root
SRC = ROOT / "VRCOSC.Modules"
OUT = ROOT / "docs"

SKIP_DIRS = {"obj", "bin", ".vs"}

MODIFIERS = (
    r"(?:public|private|protected|internal|static|abstract|virtual|override|sealed|"
    r"readonly|const|async|extern|unsafe|partial|new|volatile|required|file)"
)

TYPE_KINDS = r"(?:class|struct|interface|record|enum)"

RE_TYPE = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)"
    rf"(?P<kind>{TYPE_KINDS})\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)"
    r"(?P<generic><[^>()]*>)?"
    r"(?P<rest>[^{;=]*)"
)

# Methods: return type + name + parameter list. Excludes control-flow keywords, which
# otherwise match this shape (`if (...)`, `while (...)`, `catch (...)`, ...).
RE_METHOD = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)"
    r"(?P<ret>[A-Za-z_][A-Za-z0-9_<>\[\],\.\?\s]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)"
    r"(?P<generic><[^>()]*>)?"
    r"\s*\((?P<params>[^;]*?)\)"
    # Body token may be absent: with Allman braces the declaration line ends at `)` and
    # the `{` is on the following line. The depth guard in parse() is what keeps this from
    # matching ordinary call statements, since those only occur inside method bodies.
    r"\s*(?P<tail>(?:where[^{;]*)?)(?P<body>[{;]|=>|$)"
)

RE_CTOR = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)"
    r"(?P<name>[A-Z][A-Za-z0-9_]*)\s*\((?P<params>[^;]*?)\)"
    r"\s*(?::\s*(?:base|this)\s*\([^)]*\)\s*)?[{;]"
)

# Properties: `Type Name { ... }` or `Type Name => expr;`
RE_PROPERTY = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_<>\[\],\.\?\s]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"(?P<body>\{\s*(?:get|set|init)|=>)"
)

# Allman-style property: `Type Name` alone on the line, accessors below. Confirmed by
# lookahead rather than by pattern, since this shape is otherwise indistinguishable from
# a partial field declaration.
RE_PROPERTY_OPEN = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_<>\[\],\.\?\s]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*$"
)


def opens_property(lines: list[str], idx: int) -> bool:
    """True if the lines after idx are `{` then an accessor."""
    nxt = [l.strip() for l in lines[idx + 1:idx + 4] if l.strip()]
    return len(nxt) >= 2 and nxt[0] == "{" and re.match(r"^(get|set|init)\b", nxt[1]) is not None

RE_FIELD = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_<>\[\],\.\?\s]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?P<init>=[^;]*)?;"
)

KEYWORDS = {
    "if", "else", "for", "foreach", "while", "do", "switch", "case", "return", "throw",
    "catch", "try", "finally", "lock", "using", "fixed", "checked", "unchecked", "yield",
    "await", "new", "get", "set", "init", "value", "when", "in", "is", "as", "where",
    "namespace", "default", "nameof", "typeof", "sizeof", "stackalloc", "delegate",
}


def strip_noise(text: str) -> str:
    """Blank out comments and string literals, preserving line structure."""
    out, i, n = [], 0, len(text)
    while i < n:
        ch = text[i]
        two = text[i:i + 2]
        if two == "//":
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            i = j
        elif two == "/*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append("".join(c if c == "\n" else " " for c in text[i:j]))
            i = j
        elif ch == '"':
            # verbatim / raw / interpolated strings all end at an unescaped quote for our
            # purposes; we only need braces and keywords gone, not perfect fidelity.
            verbatim = i > 0 and text[i - 1] == "@"
            j = i + 1
            while j < n:
                if text[j] == "\\" and not verbatim:
                    j += 2
                    continue
                if text[j] == '"':
                    j += 1
                    break
                j += 1
            out.append("".join(c if c == "\n" else " " for c in text[i:j]))
            i = j
        elif ch == "'":
            j = i + 1
            while j < n and text[j] != "'":
                j += 2 if text[j] == "\\" else 1
            out.append(" " * (min(j + 1, n) - i))
            i = min(j + 1, n)
        else:
            out.append(ch)
            i += 1
    return "".join(out)


@dataclass
class Entry:
    kind: str
    name: str
    signature: str
    owner: str
    path: str
    line: int
    mods: str


def norm(s: str) -> str:
    return re.sub(r"\s+", " ", s or "").strip()


def parse(path: Path, rel: str) -> list[Entry]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    clean = strip_noise(raw)
    lines = clean.splitlines()
    raw_lines = raw.splitlines()

    entries: list[Entry] = []
    stack: list[tuple[str, int]] = []   # (type name, brace depth it opened at)
    depth = 0

    for idx, line in enumerate(lines):
        stripped = line.strip()
        raw_line = raw_lines[idx].strip() if idx < len(raw_lines) else stripped

        if not stripped or stripped.startswith("#") or stripped.startswith("["):
            depth += line.count("{") - line.count("}")
            continue

        owner = stack[-1][0] if stack else "(file scope)"

        m = RE_TYPE.match(line)
        if m and m.group("name") not in KEYWORDS:
            name = m.group("name") + (norm(m.group("generic")) or "")
            entries.append(Entry("type", name, norm(raw_line).rstrip("{").strip(),
                                 owner, rel, idx + 1, norm(m.group("mods"))))
            # Members sit one level inside the type, whether the brace is on this line
            # (K&R) or the next (Allman). Recording `depth` after counting this line's
            # braces gets Allman wrong, and this codebase is Allman throughout.
            member_depth = depth + 1
            depth += line.count("{") - line.count("}")
            if not stripped.endswith(";"):
                stack.append((name, member_depth))
            continue

        # Members live directly in the type body. Anything deeper is inside a method, so a
        # `var x = ...;` local would otherwise be counted as a field - which inflated the
        # field list roughly 15x before this check existed.
        if stack and depth == stack[-1][1]:
            handled = False

            mp = RE_PROPERTY_OPEN.match(line)
            if mp and mp.group("name") not in KEYWORDS and opens_property(lines, idx):
                entries.append(Entry("property", mp.group("name"),
                                     norm(raw_line).rstrip("{").strip(),
                                     owner, rel, idx + 1, norm(mp.group("mods"))))
                depth += line.count("{") - line.count("}")
                continue

            for regex, kind in ((RE_PROPERTY, "property"),
                                (RE_METHOD, "method"),
                                (RE_FIELD, "field")):
                mm = regex.match(line)
                if not mm:
                    continue
                name = mm.group("name")
                if name in KEYWORDS:
                    continue
                if kind == "method" and norm(mm.group("ret")).split(" ")[-1] in KEYWORDS:
                    continue
                entries.append(Entry(kind, name, norm(raw_line).rstrip("{").strip(),
                                     owner, rel, idx + 1, norm(mm.group("mods"))))
                handled = True
                break

            if not handled:
                mc = RE_CTOR.match(line)
                if mc and mc.group("name") == re.sub(r"<.*", "", owner):
                    entries.append(Entry("method", mc.group("name") + " (ctor)",
                                         norm(raw_line).rstrip("{").strip(),
                                         owner, rel, idx + 1, norm(mc.group("mods"))))

        depth += line.count("{") - line.count("}")
        while stack and depth < stack[-1][1]:
            stack.pop()

    return entries


def module_of(rel: str) -> str:
    return rel.split("/")[0] if "/" in rel else "(root)"


def visibility(mods: str) -> str:
    for v in ("public", "protected internal", "internal", "protected", "private"):
        if v in mods:
            return v
    return "private"


def write(kind: str, title: str, entries: list[Entry], blurb: str) -> None:
    rows = [e for e in entries if e.kind == kind]
    by_module: dict[str, list[Entry]] = {}
    for e in rows:
        by_module.setdefault(module_of(e.path), []).append(e)

    out = [
        f"# {title}",
        "",
        "> Generated by `gen-docs.py` — do not edit by hand. Regenerate with "
        "`python3 gen-docs.py`.",
        "",
        blurb,
        "",
        f"**{len(rows)}** total across **{len(by_module)}** modules.",
        "",
        "| Module | Count |",
        "|---|---|",
    ]
    for mod in sorted(by_module):
        out.append(f"| [{mod}](#{mod.lower()}) | {len(by_module[mod])} |")
    out.append("")

    for mod in sorted(by_module):
        out += ["", f"## {mod}", ""]
        items = by_module[mod]
        by_file: dict[str, list[Entry]] = {}
        for e in items:
            by_file.setdefault(e.path, []).append(e)

        for path in sorted(by_file):
            out += [f"### `{path}`", ""]
            if kind == "type":
                out += ["| Line | Visibility | Declaration |", "|---|---|---|"]
                for e in sorted(by_file[path], key=lambda x: x.line):
                    out.append(f"| [{e.line}](../VRCOSC.Modules/{e.path}#L{e.line}) "
                               f"| {visibility(e.mods)} | `{e.signature}` |")
            else:
                out += ["| Line | Owner | Visibility | Declaration |", "|---|---|---|---|"]
                for e in sorted(by_file[path], key=lambda x: x.line):
                    out.append(f"| [{e.line}](../VRCOSC.Modules/{e.path}#L{e.line}) "
                               f"| `{e.owner}` | {visibility(e.mods)} | `{e.signature}` |")
            out.append("")

    OUT.mkdir(exist_ok=True)
    filename = {"type": "classes", "property": "properties"}.get(kind, kind + "s")
    target = OUT / f"{filename}.md"
    target.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"{target.relative_to(ROOT)}: {len(rows)} entries")


RE_CREATE_EVENT = re.compile(
    r"CreateEvent\(\s*(?P<lookup>[A-Za-z_][A-Za-z0-9_\.]*)\s*(?:,\s*(?P<title>\"[^\"]*\"))?"
)
RE_TRIGGER_EVENT = re.compile(r"TriggerEvent\(\s*(?P<lookup>[A-Za-z_][A-Za-z0-9_\.]*)")
RE_CS_EVENT = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)event\s+(?P<type>[A-Za-z_][A-Za-z0-9_<>\[\],\.\?\s]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*[;=]"
)

# `delegate` type declarations.
RE_DELEGATE = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)delegate\s+(?P<ret>[A-Za-z_][A-Za-z0-9_<>\[\],\.\?\s]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?P<generic><[^>()]*>)?\s*\("
)

# Action/Func members used as callback hooks. Not `event`-qualified, but they are the
# same thing in practice - a slot the caller plugs a handler into - and this codebase
# uses them heavily (e.g. the `Action<Exception>? onError` pattern in LinuxUtils).
RE_CALLBACK = re.compile(
    rf"^\s*(?P<mods>(?:{MODIFIERS}\s+)*)"
    r"(?P<type>(?:Action|Func|EventHandler)(?:<[^;=]*>)?\??)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:[;={]|=>)"
)


@dataclass
class EventEntry:
    kind: str          # "chatbox" | "clr"
    name: str
    detail: str
    owner: str
    path: str
    line: int


def parse_events(path: Path, rel: str) -> list[EventEntry]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    clean_lines = strip_noise(raw).splitlines()
    raw_lines = raw.splitlines()
    found: list[EventEntry] = []

    # Track the enclosing type by brace depth. A plain "last type seen" heuristic reports
    # the wrong owner as soon as a file declares a nested enum after the members that use
    # it - CreateEvent calls in a module were being attributed to that module's *Event enum.
    stack: list[tuple[str, int]] = []
    depth = 0

    def current_owner() -> str:
        return stack[-1][0] if stack else "(file scope)"

    for idx, line in enumerate(clean_lines):
        mt = RE_TYPE.match(line)
        if mt and mt.group("name") not in KEYWORDS:
            member_depth = depth + 1
            depth += line.count("{") - line.count("}")
            if not line.strip().endswith(";"):
                stack.append((mt.group("name"), member_depth))
            continue

        owner = current_owner()

        # CreateEvent is matched on the *raw* line so the title string survives - strip_noise
        # blanks string literals, and the title is the useful part.
        mc = RE_CREATE_EVENT.search(raw_lines[idx] if idx < len(raw_lines) else "")
        if mc:
            title = (mc.group("title") or "").strip('"')
            found.append(EventEntry("chatbox", mc.group("lookup"),
                                    title or "(no title)", owner, rel, idx + 1))

        me = RE_CS_EVENT.match(line)
        if me:
            found.append(EventEntry("event", me.group("name"),
                                    norm(me.group("type")), owner, rel, idx + 1))
        else:
            md = RE_DELEGATE.match(line)
            if md:
                found.append(EventEntry(
                    "delegate", md.group("name") + (norm(md.group("generic")) or ""),
                    norm(md.group("ret")), owner, rel, idx + 1))
            else:
                mk = RE_CALLBACK.match(line)
                if mk and mk.group("name") not in KEYWORDS:
                    found.append(EventEntry("callback", mk.group("name"),
                                            norm(mk.group("type")), owner, rel, idx + 1))

        depth += line.count("{") - line.count("}")
        while stack and depth < stack[-1][1]:
            stack.pop()

    return found


def parse_triggers(path: Path, rel: str) -> list[tuple[str, str, int]]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    out = []
    for idx, line in enumerate(strip_noise(raw).splitlines()):
        for m in RE_TRIGGER_EVENT.finditer(line):
            out.append((m.group("lookup"), rel, idx + 1))
    return out


def write_events(events: list[EventEntry], triggers: list[tuple[str, str, int]]) -> None:
    """Write docs/events.md (code events) and docs/chatbox-events.md (VRCOSC events)."""
    chatbox = [e for e in events if e.kind == "chatbox"]
    code_kinds = (("event", "C# `event` declarations",
                   "Members declared with the `event` keyword — the real .NET event slots."),
                  ("delegate", "`delegate` types",
                   "Named delegate types declared in this codebase."),
                  ("callback", "Callback members (`Action` / `Func` / `EventHandler`)",
                   "Not `event`-qualified, but the same idea in practice: a slot a caller "
                   "plugs a handler into. The pervasive `Action<Exception>? onError` "
                   "parameter pattern shows up here when stored as a member."))

    # ---- docs/events.md : code events ------------------------------------------------
    out = [
        "# Code events",
        "",
        "> Generated by `tools/gen-docs.py` — do not edit by hand. Regenerate with "
        "`python3 tools/gen-docs.py`.",
        "",
        "In-code event and callback plumbing: `event` declarations, `delegate` types, and "
        "`Action`/`Func`/`EventHandler` members. These are internal wiring, invisible to "
        "users.",
        "",
        "For the user-facing VRCOSC ChatBox events (`CreateEvent` / `TriggerEvent`, "
        "bindable in the ChatBox editor) see [chatbox-events.md](chatbox-events.md).",
        "",
    ]

    for kind, title, blurb in code_kinds:
        rows = [e for e in events if e.kind == kind]
        out += [f"## {title}", "", blurb, "", f"**{len(rows)}** total.", ""]
        if not rows:
            out += ["_None._", ""]
            continue

        by_module: dict[str, list[EventEntry]] = {}
        for e in rows:
            by_module.setdefault(module_of(e.path), []).append(e)

        for mod in sorted(by_module):
            out += [f"### {mod}", "", "| Line | Owner | Name | Type |", "|---|---|---|---|"]
            for e in sorted(by_module[mod], key=lambda x: (x.path, x.line)):
                out.append(f"| [{e.line}](../VRCOSC.Modules/{e.path}#L{e.line}) "
                           f"| `{e.owner}` | `{e.name}` | `{e.detail}` |")
            out.append("")

    OUT.mkdir(exist_ok=True)
    (OUT / "events.md").write_text("\n".join(out) + "\n", encoding="utf-8")

    # ---- docs/chatbox-events.md : VRCOSC module events -------------------------------
    trig_qualified: dict[str, list[tuple[str, int]]] = {}
    trig_local: dict[tuple[str, str], list[tuple[str, int]]] = {}
    for lookup, path, line in triggers:
        if "." in lookup:
            trig_qualified.setdefault(lookup, []).append((path, line))
        else:
            trig_local.setdefault((path, lookup), []).append((path, line))

    def sites_for(e: EventEntry) -> list[tuple[str, int]]:
        out_ = list(trig_qualified.get(e.name, []))
        out_ += trig_local.get((e.path, e.name.split(".")[-1]), [])
        return sorted(set(out_))

    cb = [
        "# VRCOSC ChatBox events",
        "",
        "> Generated by `tools/gen-docs.py` — do not edit by hand. Regenerate with "
        "`python3 tools/gen-docs.py`.",
        "",
        "User-facing events registered in `OnPostLoad` via `CreateEvent(lookup, title)` "
        "and raised with `TriggerEvent`. These appear in the ChatBox editor and can be "
        "bound by users.",
        "",
        "The Fired column lists every `TriggerEvent` call site; an event with **never** "
        "there is declared but not raised. Matching prefers the fully qualified lookup, "
        "falling back to a bare name only within the same file — several modules each "
        "declare an `OnError`, so a global short-name match would cross-link them.",
        "",
        f"**{len(chatbox)}** total.",
        "",
    ]

    by_module = {}
    for e in chatbox:
        by_module.setdefault(module_of(e.path), []).append(e)

    for mod in sorted(by_module):
        cb += [f"## {mod}", "",
               "| Line | Owner | Lookup | Title | Fired |", "|---|---|---|---|---|"]
        for e in sorted(by_module[mod], key=lambda x: (x.path, x.line)):
            sites = sites_for(e)
            fired = ", ".join(f"[{Path(p).name}:{l}](../VRCOSC.Modules/{p}#L{l})"
                              for p, l in sites) or "**never**"
            cb.append(f"| [{e.line}](../VRCOSC.Modules/{e.path}#L{e.line}) | `{e.owner}` "
                      f"| `{e.name}` | {e.detail} | {fired} |")
        cb.append("")

    (OUT / "chatbox-events.md").write_text("\n".join(cb) + "\n", encoding="utf-8")

    counts = {k: len([e for e in events if e.kind == k]) for k, _, _ in code_kinds}
    print(f"docs/events.md: {counts['event']} events, {counts['delegate']} delegates, "
          f"{counts['callback']} callbacks")
    print(f"docs/chatbox-events.md: {len(chatbox)} chatbox events")


def main() -> None:
    entries: list[Entry] = []
    for path in sorted(SRC.rglob("*.cs")):
        if any(p in SKIP_DIRS for p in path.parts):
            continue
        rel = str(path.relative_to(SRC))
        entries += parse(path, rel)

    write("type", "Classes, structs, interfaces, records and enums", entries,
          "Every type declared in `VRCOSC.Modules/`, grouped by module and file.")
    write("method", "Methods", entries,
          "Every method and constructor, grouped by module and file. "
          "The Owner column is the declaring type.")
    write("property", "Properties", entries,
          "Every property (including expression-bodied and auto-properties), "
          "grouped by module and file.")
    write("field", "Fields", entries,
          "Every field, including `const` and `readonly`, grouped by module and file. "
          "Note VRCOSC node pins are declared as fields, so node types are field-heavy.")

    events: list[EventEntry] = []
    triggers: list[tuple[str, str, int]] = []
    for path in sorted(SRC.rglob("*.cs")):
        if any(p in SKIP_DIRS for p in path.parts):
            continue
        rel = str(path.relative_to(SRC))
        events += parse_events(path, rel)
        triggers += parse_triggers(path, rel)

    write_events(events, triggers)


if __name__ == "__main__":
    main()
