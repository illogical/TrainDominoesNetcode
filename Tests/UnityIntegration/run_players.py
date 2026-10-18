#!/usr/bin/env python3
"""Run the temporary integration app; retain filtered, assertion-based evidence."""
import argparse
from pathlib import Path
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument('--output-dir', type=Path, default=Path(__file__).parent, help='Directory for filtered logs; use a distinct run directory to preserve existing evidence')
parser.add_argument('--setup-failure', action='store_true', help='GAME-002: normal production authority with failing deck supplier, retry recovery, then full match')
parser.add_argument('--normal', action='store_true', help='GAME-001: production match creation and random decks; no authority injection')
parser.add_argument('--draft', action='store_true')
parser.add_argument('--presentation-only', action='store_true')
parser.add_argument('--challenge', action='store_true', help='NET-005: no announcements; opponents challenge (DrawOneTile)')
parser.add_argument('--penalty-none', action='store_true', help='with --challenge: use the None penalty')
parser.add_argument('--reconnect', action='store_true', help='NET-004: the client drops mid-action, reconnects and reclaims its seat')
parser.add_argument('--live', action='store_true', help='LIVE SERVICES: connect through Unity Lobby + Relay (needs network and permission)')
parser.add_argument('--app', default='/tmp/td-integration-build/Integration.app/Contents/MacOS/TrainDominoesNetcode')
args = parser.parse_args()
if (args.normal or args.setup_failure) and any((args.draft, args.presentation_only, args.challenge, args.penalty_none, args.reconnect, args.live)):
    parser.error('--normal is a separate localhost production startup fixture')
args.output_dir.mkdir(parents=True, exist_ok=True)
mode = 'setup-failure' if args.setup_failure else 'normal' if args.normal else 'reconnect' if args.reconnect else 'live' if args.live else 'presentation' if args.presentation_only else 'draft' if args.draft else ('challenge-none' if args.penalty_none else 'challenge') if args.challenge else 'immediate'
prefix = f'/tmp/td-integration-{mode}'
for path in Path('/tmp').glob(f'td-integration-{mode}-*'):
    if path.is_file():
        path.unlink()
processes = []
try:
    for role in ['host', 'client']:
        command = [args.app, '-batchmode', '-nographics', '--mute-audio', '-logFile', f'{prefix}-{role}.log']
        if role == 'host': command.append('--integration-host')
        if args.setup_failure: command.append('--integration-setup-failure')
        if args.normal: command.append('--integration-normal')
        if args.draft: command.append('--integration-draft')
        if args.presentation_only: command.append('--presentation-only')
        if args.challenge: command.append('--integration-challenge')
        if args.penalty_none: command.append('--integration-penalty-none')
        if args.reconnect: command.append('--integration-reconnect')
        if args.live: command += ['--integration-live', '--ugs-profile', f'td-{role}']
        processes.append(subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT))
        time.sleep(1)
    deadline = time.monotonic() + (600 if args.live else 360)
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
        (args.output_dir / f'observed-{mode}-{role}.log').write_text('\n'.join(lines)+'\n')
        if process.returncode != 0 or f'INTEGRATION {role} PASS ALL' not in log:
            raise RuntimeError(f'{role} did not pass all assertions')
    if args.reconnect:
        combined = ''.join(Path(f'{prefix}-{role}.log').read_text(errors='replace') for role in ['host', 'client'])
        for required in ['PASS host-sees-missing-seat', 'PASS same-seat-after-reconnect', 'PASS impersonation-rejected-over-rpc', 'PASS host-sees-everyone-back']:
            if required not in combined:
                raise RuntimeError(f'reconnect scenario never reached: {required}')
    if args.challenge:
        combined = ''.join(Path(f'{prefix}-{role}.log').read_text(errors='replace') for role in ['host', 'client'])
        for required in ['PASS accepted-Challenge', 'PASS expired-challenge-rejected', 'PASS self-challenge-rejected', 'PASS repeat-challenge-rejected']:
            if required not in combined:
                raise RuntimeError(f'challenge scenario never reached: {required}')
        if not args.penalty_none and 'PASS penalty-tile-received-privately' not in combined:
            raise RuntimeError('no penalty tile was received over real transport')
    print(f'PASS {mode}: both actual Unity players passed; filtered logs saved')
finally:
    for role in ['host', 'client']:
        source = Path(f'{prefix}-{role}.log')
        if source.exists():
            lines = [line for line in source.read_text(errors='replace').splitlines() if line.startswith('INTEGRATION ') or line.startswith('Exception:') or 'Presentation exceeded' in line]
            (args.output_dir / f'observed-{mode}-{role}.log').write_text('\n'.join(lines)+'\n')
    for process in processes:
        if process.poll() is None:
            process.terminate()
