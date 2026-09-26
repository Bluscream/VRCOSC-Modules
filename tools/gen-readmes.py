#!/usr/bin/env python3
"""
Dynamic VRCOSC Module Documentation Generator.

Scans C# source code files in each submodule folder to dynamically extract:
- Module Title & Description
- Module Settings (CreateTextBox, CreateToggle, CreateDropdown, CreateSlider, etc.)
- ChatBox Variables (CreateVariable<T>)
- ChatBox States (CreateState)
- ChatBox Events (CreateEvent)
- Avatar OSC Parameters (RegisterParameter<T>)
- Nodes (Node classes & [NodeTitle("...")])

Updates table sections in each submodule README.md and main README.md enclosed by HTML comment markers:
  <!-- SETTINGS_TABLE_START --> ... <!-- SETTINGS_TABLE_END -->
  <!-- VARIABLES_TABLE_START --> ... <!-- VARIABLES_TABLE_END -->
  <!-- STATES_TABLE_START --> ... <!-- STATES_TABLE_END -->
  <!-- EVENTS_TABLE_START --> ... <!-- EVENTS_TABLE_END -->
  <!-- OSC_PARAMETERS_TABLE_START --> ... <!-- OSC_PARAMETERS_TABLE_END -->
  <!-- NODES_TABLE_START --> ... <!-- NODES_TABLE_END -->
  <!-- SUBMODULES_TABLE_START --> ... <!-- SUBMODULES_TABLE_END -->

Regenerate with:  python3 tools/gen-readmes.py
"""

from __future__ import annotations

import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent  # tools/ -> repo root
BASE_DIR = ROOT / "VRCOSC.Modules"
MAIN_README_PATH = ROOT / "README.md"

def parse_csharp_module(mod_dir: Path) -> dict:
    """Dynamically parses all C# files in a submodule directory."""
    code = ""
    for cs_file in mod_dir.rglob("*.cs"):
        if "bin" in cs_file.parts or "obj" in cs_file.parts:
            continue
        try:
            code += cs_file.read_text(encoding="utf-8") + "\n"
        except Exception:
            pass

    # Title & Description
    title_match = re.search(r"\[ModuleTitle\(\"([^\"]+)\"\)\]", code)
    desc_match = re.search(r"\[ModuleDescription\(\"([^\"]+)\"\)\]", code)

    title = title_match.group(1) if title_match else mod_dir.name
    desc = desc_match.group(1) if desc_match else f"{title} module for VRCOSC."

    # Module Settings
    settings = []
    setting_pattern = re.compile(
        r"Create(TextBox|Toggle|Dropdown|Slider|KeyValuePairList|Group)\(\s*([^,]+),\s*\"([^\"]+)\"(?:,\s*\"([^\"]+)\")?(?:,\s*([^);]+))?\)",
        re.DOTALL
    )
    for match in setting_pattern.finditer(code):
        stype, skey, sname, sdesc, sdef = match.groups()
        if stype == "Group":
            continue
        desc_text = sdesc.strip() if sdesc else f"Configure {sname}"
        def_text = sdef.strip() if sdef else "empty"
        # Clean default value string
        def_text = re.sub(r"string\.Empty", "empty", def_text)
        settings.append((sname, stype, desc_text, def_text))

    # ChatBox Variables
    variables = []
    var_pattern = re.compile(r"CreateVariable<([^>]+)>\(\s*([^,]+),\s*\"([^\"]+)\"", re.DOTALL)
    for match in var_pattern.finditer(code):
        vtype, vkey, vname = match.groups()
        vtype_clean = vtype.split(".")[-1].strip()
        key_str = vkey.split(".")[-1].strip().lower() if "." in vkey else vkey.strip().strip('"')
        variables.append((vname, key_str, vtype_clean, f"ChatBox variable {vname}"))

    # ChatBox States
    states = []
    state_pattern = re.compile(r"CreateState\(\s*([^,]+),\s*\"([^\"]+)\",\s*\"([^\"]+)\"", re.DOTALL)
    for match in state_pattern.finditer(code):
        skey, sname, sfmt = match.groups()
        key_str = skey.split(".")[-1].strip().lower() if "." in skey else skey.strip().strip('"')
        fmt_clean = sfmt.replace("\n", "\\n")
        states.append((sname, key_str, fmt_clean, f"{sname} state"))

    # ChatBox Events
    events = []
    event_pattern = re.compile(r"CreateEvent\(\s*([^,]+),\s*\"([^\"]+)\"(?:,\s*\"([^\"]+)\")?", re.DOTALL)
    for match in event_pattern.finditer(code):
        ekey, ename, etitle = match.groups()
        key_str = ekey.split(".")[-1].strip().lower() if "." in ekey else ekey.strip().strip('"')
        title_str = etitle if etitle else ename
        events.append((ename, key_str, title_str, f"Triggered on {ename}"))

    # Avatar OSC Parameters
    params = []
    param_pattern = re.compile(
        r"RegisterParameter<([^>]+)>\(\s*([^,]+),\s*\"([^\"]+)\",\s*ParameterMode\.(\w+),\s*\"([^\"]+)\",\s*\"([^\"]+)\"",
        re.DOTALL
    )
    for match in param_pattern.finditer(code):
        ptype, pkey, ppath, pmode, pname, pdesc = match.groups()
        ptype_clean = ptype.split(".")[-1].strip()
        params.append((ppath, ptype_clean, pmode, pdesc))

    # Nodes Overview
    nodes = []
    node_pattern = re.compile(
        r"\[NodeTitle\(\"([^\"]+)\"\)\]\s*(?:\[NodeDescription\(\"([^\"]+)\"\)\])?",
        re.DOTALL
    )
    for match in node_pattern.finditer(code):
        ntitle, ndesc = match.groups()
        desc_text = ndesc if ndesc else f"Executes {ntitle}"
        nodes.append((ntitle, "Flow trigger", "Output", desc_text))

    # Fallback scan for classes inheriting Node if no [NodeTitle] attributes found
    if not nodes:
        class_node_pattern = re.compile(
            r"public\s+(?:sealed\s+)?class\s+(\w+Node)\s*:\s*(?:ModuleNode|FlowModuleNode|Node)",
            re.DOTALL
        )
        for match in class_node_pattern.finditer(code):
            node_class = match.group(1)
            # Friendly node title from class name (e.g. DumpAllParametersNode -> Dump All Parameters)
            friendly_name = re.sub(r"(?<!^)(?=[A-Z])", " ", node_class.replace("Node", "")).strip()
            nodes.append((friendly_name, "Flow trigger", "Output", f"Node node for {friendly_name}"))

    return {
        "title": title,
        "desc": desc,
        "settings": settings,
        "variables": variables,
        "states": states,
        "events": events,
        "osc_params": params,
        "nodes": nodes
    }

def build_table(header_cols: list[str], rows: list[tuple]) -> str:
    """Formats a clean markdown table from header columns and tuple rows."""
    lines = [
        "| " + " | ".join(header_cols) + " |",
        "|" + "|".join("---" for _ in header_cols) + "|"
    ]
    if rows:
        for r in rows:
            formatted_cells = []
            for idx, cell in enumerate(r):
                if idx in (0,): # Bold first column
                    formatted_cells.append(f"**{cell}**")
                elif idx in (1, 2, 3) and not str(cell).startswith("_"):
                    formatted_cells.append(f"`{cell}`")
                else:
                    formatted_cells.append(str(cell))
            lines.append("| " + " | ".join(formatted_cells) + " |")
    else:
        lines.append("| _None_ | " + " | ".join("—" for _ in header_cols[1:]) + " |")
    return "\n".join(lines)

def replace_or_insert_section(content: str, marker_tags: list[str], new_section_content: str, default_heading: str) -> str:
    """
    Replaces content between START and END comment markers.
    Supports both naming styles:
      <!-- SETTINGS_TABLE_START --> ... <!-- SETTINGS_TABLE_END -->
      <!-- AUTOGEN:SETTINGS:START --> ... <!-- AUTOGEN:SETTINGS:END -->
    """
    for tag in marker_tags:
        start_marker = f"<!-- {tag}_START -->"
        end_marker = f"<!-- {tag}_END -->"
        
        if start_marker in content and end_marker in content:
            start_idx = content.find(start_marker)
            end_idx = content.find(end_marker) + len(end_marker)
            block = f"{start_marker}\n{new_section_content}\n{end_marker}"
            return content[:start_idx] + block + content[end_idx:]

    # If no existing markers were found, append new section with default markers
    primary_start = f"<!-- {marker_tags[0]}_START -->"
    primary_end = f"<!-- {marker_tags[0]}_END -->"
    block = f"{primary_start}\n{new_section_content}\n{primary_end}"
    return content.strip() + f"\n\n{default_heading}\n\n{block}\n"

def generate_readmes() -> int:
    updated_count = 0
    submodules_data = {}

    for mod_dir in sorted(BASE_DIR.iterdir()):
        if not mod_dir.is_dir() or mod_dir.name.startswith((".", "bin", "obj", "Utilities")):
            continue

        data = parse_csharp_module(mod_dir)
        submodules_data[mod_dir.name] = data

        readme_file = mod_dir / "README.md"
        if readme_file.exists():
            content = readme_file.read_text(encoding="utf-8")
        else:
            content = f"# {data['title']}\n\n{data['desc']}\n\n**Repository**: https://github.com/Bluscream/VRCOSC-Modules\n"

        # 1. Settings Table
        settings_tbl = build_table(["Setting Name", "Type", "Description", "Default"], data["settings"])
        content = replace_or_insert_section(
            content,
            ["SETTINGS_TABLE", "AUTOGEN:SETTINGS"],
            settings_tbl,
            "## Module Settings"
        )

        # 2. ChatBox Variables Table
        vars_tbl = build_table(["Variable Name", "Lookup Key", "Type", "Description"], data["variables"])
        content = replace_or_insert_section(
            content,
            ["VARIABLES_TABLE", "AUTOGEN:VARIABLES"],
            vars_tbl,
            "## ChatBox Variables"
        )

        # 3. ChatBox States Table
        states_tbl = build_table(["State Name", "Lookup Key", "Format", "Description"], data["states"])
        content = replace_or_insert_section(
            content,
            ["STATES_TABLE", "AUTOGEN:STATES"],
            states_tbl,
            "## ChatBox States"
        )

        # 4. ChatBox Events Table
        events_tbl = build_table(["Event Name", "Lookup Key", "Title", "Trigger Condition"], data["events"])
        content = replace_or_insert_section(
            content,
            ["EVENTS_TABLE", "AUTOGEN:EVENTS"],
            events_tbl,
            "## ChatBox Events"
        )

        # 5. Avatar OSC Parameters Table
        params_tbl = build_table(["OSC Parameter Path", "Type", "Direction", "Description"], data["osc_params"])
        content = replace_or_insert_section(
            content,
            ["OSC_PARAMETERS_TABLE", "AUTOGEN:OSC_PARAMS"],
            params_tbl,
            "## Avatar OSC Parameters"
        )

        # 6. Nodes Overview Table
        nodes_tbl = build_table(["Node Name", "Inputs", "Outputs", "Description"], data["nodes"])
        content = replace_or_insert_section(
            content,
            ["NODES_TABLE", "AUTOGEN:NODES"],
            nodes_tbl,
            "## Nodes Overview"
        )

        readme_file.write_text(content.strip() + "\n", encoding="utf-8")
        updated_count += 1
        print(f"  [✓] Updated {mod_dir.name} README (Settings: {len(data['settings'])}, Vars: {len(data['variables'])}, States: {len(data['states'])}, Events: {len(data['events'])}, OSC Params: {len(data['osc_params'])}, Nodes: {len(data['nodes'])})")

    # Update Master README.md index table
    if MAIN_README_PATH.exists():
        main_content = MAIN_README_PATH.read_text(encoding="utf-8")
    else:
        main_content = "# Bluscream's VRCOSC Modules\n\nCustom modules for VRCOSC.\n"

    main_rows = []
    for mod_dir_name, data in sorted(submodules_data.items()):
        rel_link = f"[VRCOSC.Modules/{mod_dir_name}/README.md](VRCOSC.Modules/{mod_dir_name}/README.md)"
        s_cnt = len(data["settings"])
        v_cnt = len(data["variables"])
        st_cnt = len(data["states"])
        e_cnt = len(data["events"])
        main_rows.append((data['title'], rel_link, s_cnt, v_cnt, st_cnt, e_cnt, data['desc']))

    main_tbl = build_table(
        ["Module Name", "Folder / Docs", "Settings", "Variables", "States", "Events", "Description"],
        main_rows
    )

    main_content = replace_or_insert_section(
        main_content,
        ["SUBMODULES_TABLE", "AUTOGEN:SUBMODULES"],
        main_tbl,
        "## Submodules Index"
    )
    MAIN_README_PATH.write_text(main_content.strip() + "\n", encoding="utf-8")
    print(f"  [✓] Updated Master README.md index table.")

    return updated_count

if __name__ == "__main__":
    print("=== VRCOSC Dynamic Module README Generator ===")
    count = generate_readmes()
    print(f"Finished updating comment-marker sections across {count} submodule READMEs and main README.md.")
