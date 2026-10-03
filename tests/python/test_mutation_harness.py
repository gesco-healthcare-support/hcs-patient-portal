"""The mutation harness must be unable to pass vacuously (#1005).

A mutation harness that reports "0 survivors" because it never ran anything is the failure it
exists to prevent, wearing a new costume. So the load-bearing tests here feed it a test that is
KNOWN to be vacuous and assert it reports SURVIVED, feed it a sound one and assert KILLED, and
then walk every other route to a silent pass (empty filter, red control, mutant that does not
compile, hang, text that is not in the file) and assert each is an INVALID outcome that fails the
run rather than a quiet success.

The harness's own subject here is a tiny pure-Python guard run through the real `unittest`
runner in a real subprocess, so nothing is mocked on the path that decides KILLED versus
SURVIVED. Mocks appear only where a hard failure has to be provoked (interrupt, restore failure).
"""

import contextlib
import hashlib
import io
import json
import os
import signal
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from gate_loader import load

H = load("mutation_harness", "scripts/mutation-harness.py")

GUARD = "def can_read(user, owner):\n    if user != owner:\n        return False\n    return True\n"
SOUND = (
    "import unittest\nfrom guard import can_read\n\n\n"
    "class Sound(unittest.TestCase):\n"
    "    def test_owner_reads(self):\n        self.assertTrue(can_read('a', 'a'))\n\n"
    "    def test_stranger_is_refused(self):\n        self.assertFalse(can_read('a', 'b'))\n"
)
# Vacuous in the exact way the repo has met: it checks the allowed path only, so the
# refusal the guard exists to make is never observed. Green under the mutation.
VACUOUS = (
    "import unittest\nfrom guard import can_read\n\n\n"
    "class Vacuous(unittest.TestCase):\n"
    "    def test_owner_reads(self):\n        self.assertTrue(can_read('a', 'a'))\n"
)
BREAK_REFUSAL = {"find": "if user != owner:", "replace": "if False:"}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


class Rig(unittest.TestCase):
    """A throwaway repo, a state dir outside it, and a mutant factory."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        base = Path(self._tmp.name)
        self.root = base / "repo"
        self.root.mkdir()
        self.state = base / "state"
        self.journal = H.Journal(self.state)
        self.guard = self.root / "guard.py"
        self.write("guard.py", GUARD)
        self.write("test_sound.py", SOUND)
        self.write("test_vacuous.py", VACUOUS)
        self.write("test_empty.py", "import unittest\n")
        self.write("test_red.py", "import unittest\nfrom guard import can_read\n\n\n"
                   "class Red(unittest.TestCase):\n    def test_wrong(self):\n"
                   "        self.assertFalse(can_read('a', 'a'))\n")
        self.write("test_slow.py", "import time\nimport unittest\n\n\nclass Slow(unittest.TestCase):\n"
                   "    def test_hang(self):\n        time.sleep(60)\n")

    def write(self, name, text):
        (self.root / name).write_bytes(text.encode("utf-8"))

    def mutant(self, mid="m", filter="test_sound", **over):
        fields = dict(id=mid, file="guard.py", runner="unittest", project="", filter=filter, **BREAK_REFUSAL)
        fields.update(over)
        return H.Mutant(**fields)

    def harness(self, timeout=60.0):
        return H.Harness(self.root, self.journal, timeout)

    def run_one(self, **kw):
        return self.harness().run_one(self.mutant(**kw))

    def assertRestored(self):
        self.assertEqual(self.guard.read_text(encoding="utf-8"), GUARD)
        self.assertEqual(self.journal.entries(), [])


class TestItCanReportASurvivorAndAKill(Rig):
    def test_sound_test_is_killed_by_name(self):
        out = self.run_one(filter="test_sound", killed_by=["test_stranger_is_refused"])
        self.assertEqual((out.status, out.verdict), (H.KILLED, "OK"))
        self.assertIn("test_sound.Sound.test_stranger_is_refused", out.failed_tests)
        self.assertRestored()

    def test_vacuous_test_is_reported_as_a_survivor_and_fails_the_run(self):
        out = self.run_one(filter="test_vacuous")
        self.assertEqual((out.status, out.verdict), (H.SURVIVED, "SURVIVOR"))
        self.assertEqual(H.exit_code([out]), H.EXIT_PROBLEM)
        self.assertRestored()

    def test_a_documented_survivor_is_accepted_but_a_sound_test_declared_survivor_is_stale(self):
        accepted = self.run_one(filter="test_vacuous", expect="survive", reason="known gap")
        self.assertEqual((accepted.status, accepted.verdict), (H.SURVIVED, "OK"))
        stale = self.run_one(filter="test_sound", expect="survive", reason="known gap")
        self.assertEqual((stale.status, stale.verdict), (H.KILLED, "STALE_EXPECTATION"))
        self.assertEqual(H.exit_code([stale]), H.EXIT_PROBLEM)

    def test_a_kill_by_the_wrong_test_is_not_a_kill_of_the_claimed_guard(self):
        out = self.run_one(filter="test_sound", killed_by=["SomethingElseEntirely"])
        self.assertEqual((out.status, out.verdict), (H.KILLED_BY_OTHER, "WRONG_KILLER"))
        self.assertEqual(H.exit_code([out]), H.EXIT_PROBLEM)

    def test_same_size_mutation_in_the_same_second_is_not_hidden_by_a_stale_pyc(self):
        # `!=` -> `==` keeps the file size identical. Without a fresh bytecode cache per run,
        # a pyc keyed on (mtime seconds, size) would run the original and invent a survivor.
        for _ in range(3):
            out = self.run_one(filter="test_sound", find="if user != owner:", replace="if user == owner:")
            self.assertEqual(out.status, H.KILLED)


class TestNoRouteToASilentPass(Rig):
    def test_mutant_that_does_not_compile_is_never_a_kill(self):
        out = self.run_one(find="if user != owner:", replace="if user !=:")
        self.assertEqual((out.status, out.verdict), (H.BUILD_ERROR, "INVALID"))
        self.assertEqual(H.exit_code([out]), H.EXIT_INVALID)
        self.assertRestored()

    def test_filter_that_selects_no_tests_is_not_a_pass(self):
        out = self.run_one(filter="test_empty")
        self.assertEqual((out.status, out.verdict), (H.NO_TESTS, "INVALID"))

    def test_filter_naming_nothing_at_all_is_not_a_pass(self):
        out = self.run_one(filter="test_no_such_module")
        self.assertEqual(out.verdict, "INVALID")

    def test_red_control_makes_every_later_failure_meaningless(self):
        out = self.run_one(filter="test_red")
        self.assertEqual((out.status, out.verdict), (H.BASELINE_RED, "INVALID"))
        self.assertRestored()

    def test_find_text_absent_or_ambiguous_is_inapplicable_and_touches_nothing(self):
        before = sha(self.guard)
        for find in ("text that is not there", "r"):
            out = self.run_one(find=find, replace="x")
            self.assertEqual((out.status, out.verdict), (H.INAPPLICABLE, "INVALID"))
        self.assertEqual(sha(self.guard), before)
        self.assertEqual(H.applicability(self.root, self.mutant(file="missing.py")), "missing.py does not exist")

    def test_a_hang_is_a_timeout_not_a_kill_and_the_tree_is_killed(self):
        out = self.harness(timeout=3.0).run_one(self.mutant(filter="test_slow"))
        self.assertEqual((out.status, out.verdict), (H.TIMEOUT, "INVALID"))

    def test_time_budget_stops_starting_mutants_and_says_so(self):
        outs = H.run_all(self.harness(), [self.mutant("a"), self.mutant("b")], max_seconds=0)
        self.assertEqual([o.status for o in outs], [H.NO_RESULT, H.NO_RESULT])
        self.assertRestored()

    def test_classify_covers_every_non_verdict(self):
        self.assertEqual(H.classify(H.RunResult(timed_out=True)), H.TIMEOUT)
        self.assertEqual(H.classify(H.RunResult(build_error=True)), H.BUILD_ERROR)
        self.assertEqual(H.classify(H.RunResult(no_result=True)), H.NO_RESULT)
        self.assertEqual(H.classify(H.RunResult()), H.NO_TESTS)
        self.assertIsNone(H.classify(H.RunResult(passed={"t"})))

    def test_mutation_that_changes_nothing_is_refused_and_still_restored(self):
        same = self.mutant(find="if user != owner:", replace="if user != owner:")
        with self.assertRaisesRegex(RuntimeError, "did not change"):
            self.harness().run_one(same)
        self.assertRestored()


class TestSafeRestore(Rig):
    def test_interrupt_mid_run_restores_the_file_and_proves_it_by_hash(self):
        seen = {}
        calls = []

        def fake(root, project, filt, timeout):
            calls.append(1)
            if len(calls) == 1:
                return H.RunResult(passed={"t"})
            seen["mutated"] = self.guard.read_text(encoding="utf-8")
            raise H.Interrupted("signal 15")

        before = sha(self.guard)
        with mock.patch.dict(H.RUNNERS, {"unittest": fake}):
            with self.assertRaises(H.Interrupted):
                self.harness().run_one(self.mutant())
        self.assertIn("if False:", seen["mutated"])  # the mutation WAS live when it was cut
        self.assertEqual(sha(self.guard), before)
        self.assertRestored()

    def test_hard_kill_leaves_a_journal_that_blocks_the_next_run_until_recovered(self):
        original = self.guard.read_bytes()
        self.journal.open_entry(self.guard, original)
        self.guard.write_bytes(original.replace(b"!=", b"=="))  # killed here, no finally ran
        with self.assertRaises(H.JournalError):
            H.run_all(self.harness(), [self.mutant()])
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(H.main(["run", "--root", str(self.root), "--state-dir", str(self.state),
                                     "--manifest", str(self.manifest([self.mutant()]))]), H.EXIT_JOURNAL)
            self.assertEqual(H.main(["run", "--allow-dirty", "--root", str(self.root),
                                     "--state-dir", str(self.state),
                                     "--manifest", str(self.manifest([self.mutant()]))]), H.EXIT_JOURNAL)
            self.assertEqual(H.main(["recover", "--state-dir", str(self.state)]), H.EXIT_OK)
        self.assertEqual(self.guard.read_bytes(), original)
        self.assertEqual(self.journal.entries(), [])

    def manifest(self, mutants):
        path = self.root / "manifest.json"
        path.write_text(json.dumps({"mutants": [
            {k: v for k, v in m.__dict__.items()} for m in mutants]}), encoding="utf-8")
        return path

    def test_restore_refuses_a_missing_or_tampered_backup(self):
        entry = self.journal.open_entry(self.guard, self.guard.read_bytes())
        Path(entry["backup"]).write_bytes(b"not the original")
        with self.assertRaisesRegex(H.RestoreError, "no longer matches"):
            H.restore(entry)
        Path(entry["backup"]).unlink()
        with self.assertRaisesRegex(H.RestoreError, "missing"):
            H.restore(entry)

    def test_restore_that_does_not_hash_back_is_an_error_not_a_shrug(self):
        entry = self.journal.open_entry(self.guard, self.guard.read_bytes())
        with mock.patch.object(H, "write_bytes_durable", side_effect=lambda p, d: None):
            self.guard.write_bytes(b"still mutated")
            with self.assertRaisesRegex(H.RestoreError, "does not hash"):
                H.restore(entry)

    def test_failed_restore_keeps_the_journal_entry_and_exits_with_its_own_code(self):
        calls = []

        def fake(root, project, filt, timeout):
            calls.append(1)
            if len(calls) == 2:
                for backup in self.state.glob("*.orig"):
                    backup.unlink()
            return H.RunResult(passed={"t"})

        with mock.patch.dict(H.RUNNERS, {"unittest": fake}), \
                mock.patch.object(H, "install_signal_handlers"), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = H.main(["run", "--allow-dirty", "--root", str(self.root), "--state-dir", str(self.state),
                           "--manifest", str(self.manifest([self.mutant()]))])
        self.assertEqual(code, H.EXIT_RESTORE)
        self.assertEqual(len(self.journal.entries()), 1)  # still outstanding, so recover can see it

    def test_signal_handlers_turn_a_signal_into_an_unwind_and_exit_restores(self):
        harness = self.harness()
        names = [n for n in ("SIGINT", "SIGTERM", "SIGBREAK") if hasattr(signal, n)]
        saved = {n: signal.getsignal(getattr(signal, n)) for n in names}
        try:
            with mock.patch.object(H.atexit, "register") as reg:
                H.install_signal_handlers(harness)
            reg.assert_called_once_with(harness.emergency_restore)
            with self.assertRaises(H.Interrupted):
                signal.getsignal(signal.SIGINT)(signal.SIGINT, None)
        finally:
            for n, h in saved.items():
                signal.signal(getattr(signal, n), h)
        original = self.guard.read_bytes()
        harness._active = self.journal.open_entry(self.guard, original)
        self.guard.write_bytes(b"mutated")
        harness.emergency_restore()
        self.assertEqual(self.guard.read_bytes(), original)
        harness.emergency_restore()  # idempotent when nothing is active


class TestParsers(unittest.TestCase):
    def test_trx_counts_passes_and_every_flavour_of_failure(self):
        ns = H.TRX_NS[1:-1]
        xml = (f'<TestRun xmlns="{ns}"><Results>'
               '<UnitTestResult testName="A" outcome="Passed"/>'
               '<UnitTestResult testName="B" outcome="Failed"/>'
               '<UnitTestResult testName="C" outcome="Timeout"/>'
               '<UnitTestResult testName="D" outcome="NotExecuted"/>'
               '</Results></TestRun>')
        res = H.parse_trx(xml)
        self.assertEqual((res.passed, res.failed), ({"A"}, {"B", "C"}))

    def test_unittest_verbose_output_including_docstrings_skips_and_import_failures(self):
        out = ("test_a (m.C.test_a) ... ok\n"
               "test_b (m.C.test_b)\nA docstring line ... FAIL\n"
               "test_c (m.C.test_c) ... skipped 'why'\n"
               "test_d (m.C.test_d) ... ERROR\n"
               "test_e (m.C.test_e) ... expected failure\n"
               "test_x (unittest.loader._FailedTest.test_x) ... ERROR\n")
        res = H.parse_unittest(out)
        self.assertEqual(res.passed, {"m.C.test_a", "m.C.test_e"})
        self.assertEqual(res.failed, {"m.C.test_b", "m.C.test_d"})
        self.assertTrue(res.build_error)
        self.assertTrue(H.parse_unittest("  File x\nSyntaxError: bad\n").build_error)


class TestDotnetRunner(unittest.TestCase):
    def fake_process(self, trx_text=None, output=""):
        captured = {}

        def fake(cmd, cwd, env, timeout):
            captured.update(cmd=cmd, env=env, cwd=cwd)
            if trx_text is not None:
                folder = Path(cmd[cmd.index("--results-directory") + 1])
                (folder / "result.trx").write_text(trx_text, encoding="utf-8")
            return output, False

        return fake, captured

    def test_command_filters_by_name_and_never_inherits_the_environment_switch(self):
        ns = H.TRX_NS[1:-1]
        trx = f'<TestRun xmlns="{ns}"><UnitTestResult testName="T" outcome="Passed"/></TestRun>'
        fake, got = self.fake_process(trx)
        with mock.patch.dict(os.environ, {"DOTNET_ENVIRONMENT": "Development",
                                          "ASPNETCORE_ENVIRONMENT": "Development"}), \
                mock.patch.object(H, "run_process", fake):
            res = H.run_dotnet(Path("/r"), "t/x.csproj", "FullyQualifiedName~Foo", 5)
        self.assertEqual(res.passed, {"T"})
        self.assertIn("FullyQualifiedName~Foo", got["cmd"])
        self.assertNotIn("DOTNET_ENVIRONMENT", got["env"])
        self.assertNotIn("ASPNETCORE_ENVIRONMENT", got["env"])

    def test_no_results_file_is_a_build_error_only_when_the_build_said_so(self):
        fake, _ = self.fake_process(None, "Program.cs(3,1): error CS1002: ; expected")
        with mock.patch.object(H, "run_process", fake):
            self.assertTrue(H.run_dotnet(Path("/r"), "p", "f", 5).build_error)
        fake, _ = self.fake_process(None, "Docker teardown threw")
        with mock.patch.object(H, "run_process", fake):
            res = H.run_dotnet(Path("/r"), "p", "f", 5)
        self.assertTrue(res.no_result)
        self.assertFalse(res.build_error)


class TestProcessAndSelection(Rig):
    def test_run_process_kills_a_hung_tree_and_reports_the_timeout(self):
        code = "import time; print('up', flush=True); time.sleep(60)"
        out, timed_out = H.run_process([sys.executable, "-c", code], self.root, dict(os.environ), 2)
        self.assertTrue(timed_out)

    def test_kill_tree_tolerates_an_already_dead_process(self):
        proc = subprocess.Popen([sys.executable, "-c", "pass"])
        proc.wait()
        H.kill_tree(proc)

    def test_select_by_id_and_by_changed_paths(self):
        a, b = self.mutant("a"), self.mutant("b", file="other.py", watch=["tests/x.py"])
        self.assertEqual([m.id for m in H.select([a, b], ["b"], None)], ["b"])
        with self.assertRaisesRegex(ValueError, "unknown"):
            H.select([a, b], ["nope"], None)
        self.assertEqual([m.id for m in H.select([a, b], None, {"tests/x.py"})], ["b"])
        self.assertEqual(H.select([a, b], None, {"unrelated"}), [])

    def git(self, *args):
        subprocess.run(["git", "-c", "user.email=t@t", "-c", "user.name=t", *args],
                       cwd=self.root, check=True, capture_output=True)

    def test_dirty_target_is_refused_and_changed_files_come_from_git(self):
        self.git("init", "-q")
        self.git("add", "-A")
        self.git("commit", "-q", "-m", "base")
        H.ensure_clean(self.root, [self.mutant()])
        H.ensure_clean(self.root, [])
        self.assertEqual(H.changed_files(self.root, "HEAD"), set())
        self.guard.write_bytes(GUARD.encode() + b"# edit\n")
        with self.assertRaisesRegex(H.JournalError, "uncommitted"):
            H.ensure_clean(self.root, [self.mutant()])

    def test_state_dir_is_stable_per_checkout_and_distinct_between_them(self):
        a = H.default_state_dir(Path("/x/one"))
        self.assertEqual(a, H.default_state_dir(Path("/x/one")))
        self.assertNotEqual(a, H.default_state_dir(Path("/x/two")))


class TestManifestAndCli(Rig):
    def manifest_text(self, **over):
        raw = dict(id="m", file="guard.py", runner="unittest", filter="test_sound", **BREAK_REFUSAL)
        raw.update(over)
        return json.dumps({"mutants": [raw]})

    def load(self, text):
        path = self.root / "m.json"
        path.write_text(text, encoding="utf-8")
        return H.load_manifest(path)

    def test_manifest_rejects_the_ways_a_mutant_can_be_meaningless(self):
        for over, msg in (({"expect": "maybe"}, "expect must be"),
                          ({"expect": "survive"}, "needs a reason"),
                          ({"runner": "nunit"}, "unknown runner"),
                          ({"replace": BREAK_REFUSAL["find"]}, "mutates nothing")):
            with self.assertRaisesRegex(ValueError, msg):
                self.load(self.manifest_text(**over))
        dup = json.loads(self.manifest_text())["mutants"] * 2
        with self.assertRaisesRegex(ValueError, "duplicate"):
            self.load(json.dumps({"mutants": dup}))

    def test_the_committed_manifest_is_valid_and_every_find_text_still_applies(self):
        mutants = H.load_manifest(H.DEFAULT_MANIFEST)
        self.assertGreaterEqual(len(mutants), 5)
        for m in mutants:
            # Read the COMMITTED file, not the working copy: a harness run in progress has the
            # working copy deliberately mutated, and this test must not flake against it.
            committed = subprocess.run(["git", "show", f"HEAD:{m.file}"], cwd=H.REPO_ROOT,
                                       capture_output=True, check=True).stdout
            self.assertEqual(committed.count(m.find.encode("utf-8")), 1, m.id)
        self.assertIn("survive", {m.expect for m in mutants})  # the canary is what proves survival is reportable

    def cli(self, *args):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf), contextlib.redirect_stderr(buf),                 mock.patch.object(H, "install_signal_handlers"):
            code = H.main(["--state-dir", str(self.state), "--root", str(self.root),
                           "--manifest", str(self.root / "m.json"), *args])
        return code, buf.getvalue()

    def test_cli_run_list_check_and_report(self):
        (self.root / "m.json").write_text(self.manifest_text(), encoding="utf-8")
        report = self.root / "r.json"
        code, out = self.cli("run", "--allow-dirty", "--report", str(report))
        self.assertEqual(code, H.EXIT_OK)
        self.assertIn("1/1 killed", out)
        self.assertEqual(json.loads(report.read_text(encoding="utf-8"))[0]["status"], H.KILLED)
        self.assertEqual(self.cli("list")[0], H.EXIT_OK)
        self.assertEqual(self.cli("check-manifest")[0], H.EXIT_OK)
        self.guard.write_text("def can_read(u, o):\n    return True\n", encoding="utf-8")
        code, out = self.cli("check-manifest")
        self.assertEqual(code, H.EXIT_INVALID)
        self.assertIn("STALE m", out)

    def test_cli_survivor_exits_one_and_names_itself(self):
        (self.root / "m.json").write_text(self.manifest_text(filter="test_vacuous"), encoding="utf-8")
        code, out = self.cli("run", "--allow-dirty", "--max-seconds", "600")
        self.assertEqual(code, H.EXIT_PROBLEM)
        self.assertIn("SURVIVOR", out)

    def test_cli_refuses_to_run_where_it_cannot_prove_the_original_is_committed(self):
        (self.root / "m.json").write_text(self.manifest_text(), encoding="utf-8")
        code, out = self.cli("run")  # not a git checkout, no --allow-dirty
        self.assertEqual(code, H.EXIT_JOURNAL)
        self.assertIn("cannot verify", out)
        self.assertRestored()

    def test_cli_changed_selects_via_git(self):
        (self.root / "m.json").write_text(self.manifest_text(), encoding="utf-8")
        with mock.patch.object(H, "changed_files", return_value=set()):
            code, out = self.cli("list", "--changed", "origin/main")
        self.assertEqual((code, out.strip()), (H.EXIT_OK, ""))

    def test_render_reports_counts_and_detail_for_a_problem(self):
        text = H.render([H.Outcome("a", H.KILLED, "OK"), H.Outcome("b", H.SURVIVED, "SURVIVOR", "why", seconds=1.5)])
        self.assertIn("1/2 killed", text)
        self.assertIn("1/2 as expected", text)
        self.assertIn("    why", text)


if __name__ == "__main__":
    unittest.main()
