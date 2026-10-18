#!/usr/bin/env python3
"""Run the temporary integration app; retain filtered, assertion-based evidence."""
import argparse
from pathlib import Path
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument('--draft', action='store_true')
parser.add_argument('--presentation-only', action='store_true')
parser.add_argument('--app', default='/tmp/td-integration-build/Integration.app/Contents/MacOS/TrainDominoesNetcode')
args = parser.parse_args()
mode = 'presentation' if args.presentation_only else 'draft' if args.draft else 'immediate'
prefix = f'/tmp/td-integration-{mode}'
for path in Path('/tmp').glob(f'td-integration-{mode}-*'):
    if path.is_file():
        path.unlink()
processes = []
try:
    for role in ['host', 'client']:
        command = [args.app, '-batchmode', '-nographics', '-logFile', f'{prefix}-{role}.log']
        if role == 'host': command.append('--integration-host')
        if args.draft: command.append('--integration-draft')
        if args.presentation_only: command.append('--presentation-only')
        processes.append(subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT))
        time.sleep(1)
    deadline = time.monotonic() + 360
    while any(p.poll() is None for p in processes):
        logs = [Path(f'{prefix}-{role}.log').read_text(errors='replace') if Path(f'{prefix}-{role}.log').exists() else '' for role in ['host', 'client']]
        if any('Exception: ASSERT' in log or 'Exception: TIMEOUT' in log for log in logs):
            raise RuntimeError('Runtime assertion failed; inspect player logs')
        if time.monotonic() > deadline:
            raise RuntimeError('Runtime verification exceeded 360 seconds')
        time.sleep(1)
    for role, process in zip(['host', 'client'], processes):
        log = Path(f'{prefix}-{role}.log').read_text(errors='replace')
        lines = [line for line in log.splitlines() if line.startswith('INTEGRATION ') or line.startswith('Exception:') or 'Presentation exceeded' in line]
        Path(__file__).with_name(f'observed-{mode}-{role}.log').write_text('\n'.join(lines)+'\n')
        if process.returncode != 0 or f'INTEGRATION {role} PASS ALL' not in log:
            raise RuntimeError(f'{role} did not pass all assertions')
    print(f'PASS {mode}: both actual Unity players passed; filtered logs saved')
finally:
    for role in ['host', 'client']:
        source = Path(f'{prefix}-{role}.log')
        if source.exists():
            lines = [line for line in source.read_text(errors='replace').splitlines() if line.startswith('INTEGRATION ') or line.startswith('Exception:') or 'Presentation exceeded' in line]
            Path(__file__).with_name(f'observed-{mode}-{role}.log').write_text('\n'.join(lines)+'\n')
    for process in processes:
        if process.poll() is None:
            process.terminate()
