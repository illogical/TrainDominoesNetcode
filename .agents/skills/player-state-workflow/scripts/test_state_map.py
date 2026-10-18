import copy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import state_map as maps


FIXTURE = """
class GameStateBase {}
abstract class Shared : GameStateBase {
    public virtual void EnterState() { ctx.Events.End += OnEnd; }
    public virtual void LeaveState() { ctx.Events.End -= OnEnd; }
    void OnEnd() { ctx.SwitchState(c => new B(c)); }
}
class A : Shared {
    public override void EnterState() { base.EnterState(); ctx.Events.Added += OnAdded; }
    public override void LeaveState() { ctx.Events.Added -= OnAdded; base.LeaveState(); }
    void OnAdded() {
        // ctx.SwitchState(c => new CommentOnly(c));
        var text = "ctx.SwitchState(c => new StringOnly(c));";
        if (ctx.IsGroupTurn) { return; }
        Move();
    }
    void Move() { ctx.SwitchState(c => new B(c), reuse: false); }
}
class B : Shared {}
class Unused : GameStateBase {}
class GameStateContext { public GameStateContext() { SwitchState(c => new A(c)); } }
"""


class StateMapChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dll = maps.build_analyzer()

    def inventory(self, source=FIXTURE):
        result = maps.analyze([{"path": "Assets/Scripts/Game/States/Fixture.cs", "text": source}], self.dll)
        result.update(schemaVersion=1, status="source", sourceLabel="Fixture", sourceHash="fixture-hash")
        maps.validate_map(result)
        return result

    def test_inheritance_guards_helpers_and_comments(self):
        data = self.inventory()
        self.assertEqual([], data["warnings"])
        self.assertEqual(4, len(data["transitions"]))
        placed = next(e for e in data["transitions"] if e["event"] == "ctx.Events.Added")
        self.assertEqual(("A", "B", "!(ctx.IsGroupTurn)", False, "Move"),
                         tuple(placed[k] for k in ("source", "target", "guard", "reuse", "handler")))
        shared = [e for e in data["transitions"] if e["owner"] == "Shared"]
        self.assertEqual({"A", "B"}, {e["source"] for e in shared})
        self.assertEqual(["Unused"], maps.unreachable(data))
        self.assertNotIn("CommentOnly", maps.report(data))
        self.assertNotIn("StringOnly", maps.report(data))
        self.assertIn("g_Shared --> s_B", maps.mermaid(data))

    def test_override_without_base_entry_does_not_inherit_subscriptions(self):
        data = self.inventory(FIXTURE.replace("base.EnterState();", ""))
        self.assertFalse(any(e["source"] == "A" and e["owner"] == "Shared" for e in data["transitions"]))
        self.assertTrue(any(e["source"] == "B" and e["owner"] == "Shared" for e in data["transitions"]))

    def test_unbalanced_subscriptions_are_visible(self):
        data = self.inventory(FIXTURE.replace("ctx.Events.Added -= OnAdded;", ""))
        self.assertTrue(any("no matching LeaveState unsubscribe" in w for w in data["warnings"]))

    def test_unresolved_factory_deferred_calls_and_compilation_are_flagged(self):
        for source, expected in [
            (FIXTURE.replace("ctx.SwitchState(c => new B(c), reuse: false)", "ctx.SwitchState(factory)"), "Unresolved transition factory"),
            (FIXTURE.replace("Move();", "Schedule(() => ctx.SwitchState(c => new B(c)));"), "Deferred lambda"),
            ("#if UNITY_EDITOR\n" + FIXTURE + "\n#endif", "conditional compilation"),
            (FIXTURE.replace("Move();", "while (ready) { Move(); }"), "Unsupported control flow"),
        ]:
            with self.subTest(expected=expected):
                data = self.inventory(source)
                self.assertTrue(any(expected in w for w in data["warnings"]), data["warnings"])

    def test_proposal_adds_inherited_exits_and_keeps_original_unchanged(self):
        before = self.inventory()
        original = copy.deepcopy(before)
        proposal = dict(title="Illustrative processing step", add_states=[dict(name="Processing", base="Shared", inheritFrom="Shared")],
                        add_transitions=[dict(source="A", event="ctx.Events.Request", target="Processing"),
                                         dict(source="Processing", event="Accepted", target="B")])
        after = maps.proposal_map(before, proposal)
        self.assertEqual(original, before)
        self.assertEqual("proposed", after["status"])
        self.assertTrue(any(e["source"] == "Processing" and e["owner"] == "Shared" for e in after["transitions"]))
        self.assertIn("## Before", maps.comparison(before, after))
        self.assertIn("## After", maps.comparison(before, after))
        self.assertIn("Processing", maps.comparison(before, after))

    def test_state_removal_requires_explicit_incident_edge_removal(self):
        before = self.inventory()
        with self.assertRaisesRegex(ValueError, "Dangling"):
            maps.proposal_map(before, dict(title="Remove B", remove_states=["B"]))
        edges = [{k: e[k] for k in maps.EDGE_FIELDS} for e in before["transitions"] if e["source"] == "B" or e["target"] == "B"]
        after = maps.proposal_map(before, dict(title="Remove B", remove_states=["B"], remove_transitions=edges))
        self.assertNotIn("B", [n["name"] for n in after["states"]])

    def test_invalid_or_ambiguous_proposals_fail(self):
        before = self.inventory()
        for proposal in [dict(title="Duplicate", add_states=[dict(name="A")]),
                         dict(title="Typo", add_transition=[]),
                         dict(title="Unknown target", add_transitions=[dict(source="A", event="Request", target="Unknown")]),
                         dict(title="No match", remove_transitions=[dict(source="A", event="NotThere", target="B")])]:
            with self.subTest(proposal=proposal), self.assertRaises(ValueError):
                maps.proposal_map(before, proposal)

    def test_line_number_changes_do_not_show_as_changed_transitions(self):
        before = self.inventory()
        after = self.inventory("\n\n" + FIXTURE)
        self.assertEqual({maps.edge_key(e) for e in before["transitions"]}, {maps.edge_key(e) for e in after["transitions"]})
        self.assertIn("## Removed transitions\n\nNone.", maps.comparison(before, after))
        self.assertIn("## Added transitions\n\nNone.", maps.comparison(before, after))

    def test_committed_ref_and_working_copy_map_different_sources(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            def git(*args):
                return maps.run(["git", *args], cwd=repo)
            git("init", "-q")
            source = repo / maps.STATE_DIR / "Fixture.cs"
            source.parent.mkdir(parents=True)
            source.write_text(FIXTURE)
            git("add", ".")
            git("-c", "user.name=State Map Check", "-c", "user.email=state-map@example.invalid", "commit", "-qm", "fixture")
            source.write_text(FIXTURE.replace("ctx.IsGroupTurn", "ctx.OtherGuard"))
            committed = maps.source_map(repo, "HEAD")
            working = maps.source_map(repo)
            self.assertNotEqual(committed["sourceHash"], working["sourceHash"])
            self.assertTrue(any("OtherGuard" in e["guard"] for e in working["transitions"]))
            self.assertFalse(any("OtherGuard" in e["guard"] for e in committed["transitions"]))


if __name__ == "__main__":
    unittest.main()
