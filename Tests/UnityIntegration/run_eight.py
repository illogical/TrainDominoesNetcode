#!/usr/bin/env python3
"""Run eight local real Unity players without contacting multiplayer services."""
from pathlib import Path
import subprocess
import time

app = '/tmp/td-integration-build/Integration.app/Contents/MacOS/TrainDominoesNetcode'
for path in Path('/tmp').glob('td-eight-*'):
    if path.is_file(): path.unlink()
processes = []
try:
    for index in range(8):
        command = [app, '-batchmode', '-nographics', '--integration-eight', '-logFile', f'/tmp/td-eight-{index}.log']
        if index == 0: command.append('--integration-host')
        processes.append(subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT))
        time.sleep(1)
    deadline = time.monotonic() + 150
    while any(p.poll() is None for p in processes):
        logs = [Path(f'/tmp/td-eight-{i}.log').read_text(errors='replace') for i in range(8)]
        if any('Exception: ASSERT' in log or 'Exception: TIMEOUT' in log for log in logs):
            raise RuntimeError('Eight-player runtime assertion failed; inspect logs')
        if time.monotonic() > deadline: raise RuntimeError('Eight-player run timed out')
        time.sleep(1)
    for index, process in enumerate(processes):
        log = Path(f'/tmp/td-eight-{index}.log').read_text(errors='replace')
        if process.returncode != 0 or 'PASS ALL' not in log:
            raise RuntimeError(f'Player {index} did not pass')
    print('PASS eight actual Unity players: deal, action, suspension and seven-seat replacement')
finally:
    for index, process in enumerate(processes):
        source = Path(f'/tmp/td-eight-{index}.log')
        if source.exists():
            lines = [line for line in source.read_text(errors='replace').splitlines() if line.startswith('EIGHT ') or line.startswith('Exception:') or 'Presentation exceeded' in line]
            Path(__file__).with_name(f'observed-eight-{index}.log').write_text('\n'.join(lines)+'\n')
        if process.poll() is None: process.terminate()
