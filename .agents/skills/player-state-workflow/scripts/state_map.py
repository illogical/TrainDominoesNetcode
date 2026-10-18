#!/usr/bin/env python3
"""Generate source inventories and proposal previews; never edit gameplay sources."""
import argparse
import copy
import hashlib
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
STATE_DIR = "Assets/Scripts/Game/States"
EDGE_FIELDS = ("source", "event", "target", "guard", "reuse", "handler", "owner")


def run(args, **kwargs):
    result = subprocess.run(args, text=True, capture_output=True, **kwargs)
    if result.returncode:
        raise ValueError(f"Command failed: {' '.join(map(str, args))}\n{result.stdout}\n{result.stderr}")
    return result.stdout


def build_analyzer():
    project = HERE / "StateMap" / "StateMap.csproj"
    fingerprint = hashlib.sha256(b"".join((project.parent / name).read_bytes()
                                        for name in ("StateMap.csproj", "Program.cs", "NuGet.Config"))
                                 + run(["dotnet", "--version"]).encode()).hexdigest()[:16]
    cache = Path(tempfile.gettempdir()) / "train-dominoes-state-map" / fingerprint
    run(["dotnet", "build", str(project), "--nologo", "--disable-build-servers",
         "-p:UseSharedCompilation=false", "-p:BaseIntermediateOutputPath=" + str(cache / "obj") + "/",
         "-p:OutputPath=" + str(cache / "bin") + "/", "-v:q"])
    return cache / "bin" / "StateMap.dll"


def analyze(files, dll=None):
    dll = dll or build_analyzer()
    return json.loads(run(["dotnet", str(dll)], input=json.dumps(files)))


def source_map(repo, ref=None):
    repo = Path(repo).resolve()
    head = run(["git", "rev-parse", "HEAD"], cwd=repo).strip()
    if ref:
        revision = run(["git", "rev-parse", "--verify", "--end-of-options", ref + "^{commit}"], cwd=repo).strip()
        names = run(["git", "ls-tree", "-rz", "--name-only", revision, "--", STATE_DIR], cwd=repo).split("\0")
        files = [{"path": name, "text": run(["git", "show", f"{revision}:{name}"], cwd=repo)}
                 for name in sorted(names) if name.endswith(".cs")]
        label = f"Git {revision}"
    else:
        files = [{"path": p.relative_to(repo).as_posix(), "text": p.read_text(encoding="utf-8-sig")}
                 for p in sorted((repo / STATE_DIR).glob("*.cs"))]
        label = f"Working tree (HEAD {head})"
    if not files:
        raise ValueError(f"No player-state C# files found at {STATE_DIR}")
    inventory = analyze(files)
    inventory.update(schemaVersion=1, status="source", sourceLabel=label,
                     sourceHash=hashlib.sha256(json.dumps(files, sort_keys=True).encode()).hexdigest())
    validate_map(inventory)
    return inventory


def edge_key(edge):
    return tuple(edge.get(field) for field in EDGE_FIELDS)


def validate_map(data):
    if data.get("schemaVersion") != 1:
        raise ValueError("Expected state map schemaVersion 1")
    names = [node["name"] for node in data["states"]]
    if len(names) != len(set(names)):
        raise ValueError("Duplicate state names")
    for name in names:
        if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
            raise ValueError(f"Unsupported state identifier: {name}")
    concrete = {n["name"] for n in data["states"] if not n["abstract"]}
    for edge in data["transitions"]:
        if edge["source"] not in concrete | {"[*]"} or edge["target"] not in concrete:
            raise ValueError(f"Dangling or abstract transition endpoint: {edge}")
    keys = [edge_key(edge) for edge in data["transitions"]]
    if len(keys) != len(set(keys)):
        raise ValueError("Duplicate transitions")


def proposal_map(before, proposal):
    allowed = {"title", "impacts", "add_states", "remove_states", "add_transitions", "remove_transitions"}
    if set(proposal) - allowed or not isinstance(proposal.get("title"), str) or not proposal["title"].strip():
        raise ValueError("Proposal requires a title and only supported edit fields; see references/preview-format.md")
    after = copy.deepcopy(before)
    after.update(status="proposed", sourceLabel="PROPOSED: " + proposal["title"],
                 proposalImpacts=proposal.get("impacts", []))
    if not isinstance(after["proposalImpacts"], list) or not all(isinstance(x, str) for x in after["proposalImpacts"]):
        raise ValueError("impacts must be an array of strings")
    for selector in proposal.get("remove_transitions", []):
        if not {"source", "event", "target"} <= selector.keys() or set(selector) - set(EDGE_FIELDS):
            raise ValueError("Transition removal needs source, event, target; optional guard/reuse/handler/owner")
        matches = [e for e in after["transitions"] if all(e.get(k) == v for k, v in selector.items())]
        if len(matches) != 1:
            raise ValueError(f"Removal must match exactly one transition (matched {len(matches)}): {selector}")
        after["transitions"].remove(matches[0])
    for name in proposal.get("remove_states", []):
        if not any(n["name"] == name for n in after["states"]):
            raise ValueError(f"Cannot remove unknown state: {name}")
        after["states"] = [n for n in after["states"] if n["name"] != name]
        after["reactions"] = [r for r in after["reactions"] if r["source"] != name]
    for node in proposal.get("add_states", []):
        if set(node) - {"name", "base", "inheritFrom"} or "name" not in node:
            raise ValueError("New state requires name; optional base and inheritFrom")
        if any(n["name"] == node["name"] for n in after["states"]):
            raise ValueError(f"State already exists: {node['name']}")
        parent = node.get("base", "GameStateBase")
        if parent != "GameStateBase" and not any(n["name"] == parent and n["abstract"] for n in after["states"]):
            raise ValueError(f"Unknown abstract base: {parent}")
        after["states"].append(dict(name=node["name"], base=parent, abstract=False, file="<proposal>", line=0))
        inherited = node.get("inheritFrom")
        if inherited:
            if inherited != parent:
                raise ValueError("inheritFrom must equal base; it asserts that proposed entry/exit call the base lifecycle")
            templates = {tuple(e.get(k) for k in EDGE_FIELDS if k != "source"): e
                         for e in before["transitions"] if e["owner"] == inherited}
            if not templates:
                raise ValueError(f"No known transition templates for inherited owner {inherited}")
            after["transitions"].extend(dict(e, source=node["name"], file="<proposal>", line=0) for e in templates.values())
            shared = {(r["event"], r["handler"]): r for r in before["reactions"] if r["owner"] == inherited}
            after["reactions"].extend(dict(r, source=node["name"], file="<proposal>", line=0) for r in shared.values())
        elif parent != "GameStateBase":
            after["warnings"].append(f"PROPOSED {node['name']}: base {parent} lifecycle inheritance is unspecified; shared exits were not inferred.")
    for edge in proposal.get("add_transitions", []):
        if not {"source", "event", "target"} <= edge.keys() or set(edge) - set(EDGE_FIELDS):
            raise ValueError("New transition requires source, event, target; optional guard/reuse/handler/owner")
        if "reuse" in edge and not isinstance(edge["reuse"], bool):
            raise ValueError("reuse must be boolean")
        after["transitions"].append(dict(guard="", reuse=True, handler="<proposed>", owner=edge["source"],
                                         **{k: v for k, v in edge.items() if k not in {"guard", "reuse", "handler", "owner"}},
                                         file="<proposal>", line=0))
        after["transitions"][-1].update(edge)
    after["transitions"].sort(key=lambda e: tuple(str(v) for v in edge_key(e)))
    after["states"].sort(key=lambda n: n["name"])
    validate_map(after)  # Removed states must have all incoming/outgoing edges explicitly handled.
    return after


def text(value):
    return str(value or "—").replace("|", "&#124;").replace("\n", " ").replace("`", "'")


def label(value):
    return str(value).replace("ctx.GameplayManager.TurnManager.", "").split("ctx.GameplayManager.")[-1].split("ctx.GameSession.")[-1].replace('"', "'").replace("\n", " ").replace(":", " ").replace(";", ",").replace("#", "number ")


def unreachable(data):
    reached = {"[*]"}
    while True:
        new = {e["target"] for e in data["transitions"] if e["source"] in reached} - reached
        if not new:
            return sorted(n["name"] for n in data["states"] if not n["abstract"] and n["name"] not in reached)
        reached |= new


def mermaid(data):
    nodes = {n["name"]: n for n in data["states"]}
    groups = {n["name"]: sorted(c["name"] for c in nodes.values() if not c["abstract"] and c["base"] == n["name"])
              for n in nodes.values() if n["abstract"]}
    groups = {k: v for k, v in groups.items() if v}
    grouped = {child for children in groups.values() for child in children}
    lines = ["stateDiagram-v2"]
    for parent, children in sorted(groups.items()):
        lines.append(f'    state "{parent} subclasses" as g_{parent} {{')
        for child in children:
            lines.append(f'        state "{child}" as s_{child}')
        lines.append("    }")
    for name in sorted(n for n in nodes if not nodes[n]["abstract"] and n not in grouped):
        lines.append(f'    state "{name}" as s_{name}')
    # Lifetime coordinator routes share an outer group, retaining nested inheritance.
    coordinator = "PlayerStateCoordinator"
    concrete = {n["name"] for n in nodes.values() if not n["abstract"]}
    coordinator_edges = [e for e in data["transitions"] if e["owner"] == coordinator]
    if coordinator_edges:
        lines = [lines[0], f'    state "{coordinator} possible routes" as g_{coordinator} {{'] + ["    " + line for line in lines[1:]] + ["    }"]
    emitted = set()
    for edge in data["transitions"]:
        source = "[*]" if edge["source"] == "[*]" else "s_" + edge["source"]
        shared = sorted(concrete) if edge["owner"] == coordinator else groups.get(edge["owner"], [])
        key = tuple(edge.get(k) for k in EDGE_FIELDS if k != "source")
        if edge["source"] in shared and all(any(e["source"] == child and tuple(e.get(k) for k in EDGE_FIELDS if k != "source") == key
                                                   for e in data["transitions"]) for child in shared):
            source = "g_" + edge["owner"]
        trigger = edge["event"].split(".")[-1]
        description = label(trigger)
        if edge["guard"]:
            description += " / " + label(edge["guard"])
        if not edge["reuse"]:
            description += " / fresh instance"
        rendered = f"    {source} --> s_{edge['target']}: {description}"
        if rendered not in emitted:
            emitted.add(rendered)
            lines.append(rendered)
    for name in unreachable(data):
        lines.extend([f"    note right of s_{name}", "        No static route from initialization", "    end note"])
    return "\n".join(lines)


def edge_table(edges):
    lines = ["| From | Trigger | Guard | To | Instance | Handler / location |", "| --- | --- | --- | --- | --- | --- |"]
    for edge in edges:
        lines.append("| " + " | ".join(text(v) for v in (edge["source"], edge["event"], edge["guard"], edge["target"],
                                                        "cached / re-enter" if edge["reuse"] else "fresh",
                                                        f"{edge['owner']}.{edge['handler']} ({edge['file']}:{edge['line']})")) + " |")
    return "\n".join(lines) if edges else "None."


def report(data):
    lines = ["# Player-state flow — " + ("proposed" if data["status"] == "proposed" else "current source"), "", data["sourceLabel"],
             "", "State-source SHA-256: `" + data["sourceHash"] + "`.", "",
             "Static source inventory, not runtime verification or an authoritative rules specification. Guards are source expressions, not evaluated predicates. RPC requests do not imply acceptance.",
             "", "```mermaid", mermaid(data), "```", "",
             "Boxes group shared inheritance; they are not additional runtime states. Shared arrows summarize matching inherited handlers; the table expands every concrete source. Coordinator arrows are possible routes from every concrete state; their guards determine runtime applicability. Self-arrows denote switch requests; coordinator code may suppress same-mode re-entry.",
             "", "## Transitions", "", edge_table(data["transitions"]), "", "## Event reactions", "",
             "Includes input/actions that may send requests without changing state. Actions are direct call-site inventory; consult source for guards and indirect helper effects.", "",
             "| State | Event | Handler | Direct actions |", "| --- | --- | --- | --- |"]
    for reaction in data["reactions"]:
        lines.append("| " + " | ".join(text(v) for v in (reaction["source"], reaction["event"], reaction["handler"], "; ".join(reaction["actions"]))) + " |")
    lines += ["", "## Coverage and review", "", "No static initialization route: " + (", ".join(unreachable(data)) or "none") + ".",
              "", "Sources are limited to `Assets/Scripts/Game/States`. The named `PlayerStateCoordinator` lifetime router maps Start, direct Start/Dispose subscriptions, and public Reconcile; external callers of Reconcile are not traced. Other unreached SwitchState calls in these files warn. Transitions outside these files, event emission/targeting, runtime ordering, dynamically created callbacks, and arbitrary control flow require review.", ""]
    lines += ["- " + warning for warning in data["warnings"]] or ["No extractor warnings for the recognized source patterns; this is not a completeness proof."]
    return "\n".join(lines) + "\n"


def comparison(before, after):
    old = {edge_key(e): e for e in before["transitions"]}
    new = {edge_key(e): e for e in after["transitions"]}
    old_nodes = {n["name"]: n for n in before["states"]}
    new_nodes = {n["name"]: n for n in after["states"]}
    lines = ["# Player-state change preview", "", f"Before: {before['sourceLabel']}", f"After: {after['sourceLabel']}", "",
             "Proposed after-maps are design overlays, not implemented or verified gameplay.", "",
             "Added states: " + (", ".join(sorted(new_nodes.keys() - old_nodes.keys())) or "none"),
             "Removed states: " + (", ".join(sorted(old_nodes.keys() - new_nodes.keys())) or "none"), ""]
    for name in sorted(old_nodes.keys() & new_nodes.keys()):
        if (old_nodes[name]["base"], old_nodes[name]["abstract"]) != (new_nodes[name]["base"], new_nodes[name]["abstract"]):
            lines.append(f"Changed inheritance: {name}: {old_nodes[name]['base']} -> {new_nodes[name]['base']}")
    lines += ["", "## Before", "", "```mermaid", mermaid(before), "```", "", "## After", "", "```mermaid", mermaid(after), "```",
              "", "## Removed transitions", "", edge_table([old[k] for k in sorted(old.keys() - new.keys(), key=str)]),
              "", "## Added transitions", "", edge_table([new[k] for k in sorted(new.keys() - old.keys(), key=str)])]
    old_reactions = {(r["source"], r["event"], r["handler"], tuple(r["actions"])) for r in before["reactions"]}
    new_reactions = {(r["source"], r["event"], r["handler"], tuple(r["actions"])) for r in after["reactions"]}
    lines += ["", "## Event reaction changes", "", f"Removed reaction/action records: {len(old_reactions - new_reactions)}; added: {len(new_reactions - old_reactions)}.",
              "Inspect before.md / after.md for the action inventories; proposals only model explicitly declared edits.", "",
              "## Design ramifications to review", ""]
    lines += ["- " + item for item in after.get("proposalImpacts", [])] or ["- Review input permissions, command/acknowledgment flow, shared/terminal exits, subscriptions, reset/undo, and tests for each changed edge."]
    lines += ["", "## Coverage warnings", ""]
    lines += ["- " + text(w) for w in sorted(set(before["warnings"] + after["warnings"]))] or ["No extractor warnings. Planning notes and new behavior still require source/rules review."]
    return "\n".join(lines) + "\n"


def write_map(directory, name, data):
    (directory / (name + ".json")).write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n")
    (directory / (name + ".md")).write_text(report(data))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("map", "preview", "compare"):
        command = commands.add_parser(name)
        command.add_argument("--output", type=Path, required=True, help="Explicit output directory; use /tmp during planning")
        command.add_argument("--strict", action="store_true", help="Return 2 if extraction has coverage warnings")
        if name != "compare":
            command.add_argument("--repo", default=".")
            command.add_argument("--ref", help="Read a Git commit/ref instead of the working tree")
        if name == "preview":
            command.add_argument("--proposal", type=Path, required=True)
        if name == "compare":
            command.add_argument("--before", type=Path, required=True)
            command.add_argument("--after", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "compare":
        before = json.loads(args.before.read_text())
        after = json.loads(args.after.read_text())
        validate_map(before)
        validate_map(after)
    else:
        before = source_map(args.repo, args.ref)
        after = proposal_map(before, json.loads(args.proposal.read_text())) if args.command == "preview" else before
    args.output.mkdir(parents=True, exist_ok=True)
    if args.command == "map":
        write_map(args.output, "current", before)
    else:
        write_map(args.output, "before", before)
        write_map(args.output, "after", after)
        (args.output / "preview.md").write_text(comparison(before, after))
    print(f"Wrote {args.command} artifacts to {args.output.resolve()}")
    for warning in sorted(set(before["warnings"] + after["warnings"])):
        print("WARNING: " + warning, file=sys.stderr)
    return 2 if args.strict and (before["warnings"] or after["warnings"]) else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, KeyError, TypeError) as error:
        print(f"state_map: {error}", file=sys.stderr)
        sys.exit(1)
