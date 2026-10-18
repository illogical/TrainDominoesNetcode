#!/usr/bin/env python3
"""VIEW-001–004 probes: one actual Unity player per window size and player count (no network, muted).

Batchmode players keep a 640x480 screen, so ViewRuntime renders the real camera into an offscreen target of each
requested size, with the canvases in that camera's space. Default runs use -nographics. --screenshots keeps a Metal
device (still batchmode, no window) and saves PNGs for human-visible review; screenshots are not a pass/fail check.
"""
import argparse
from pathlib import Path
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument('--app', default='/tmp/td-integration-build/Integration.app/Contents/MacOS/TrainDominoesNetcode')
parser.add_argument('--sizes', default='1280x720,1920x1080,2532x1170')
parser.add_argument('--players', default='2,3,8')
parser.add_argument('--screenshots', action='store_true')
parser.add_argument('--shots', default='/tmp/td-view-shots')
args = parser.parse_args()
evidence = []
failed = []
for size in args.sizes.split(','):
    width, height = size.split('x')
    for players in args.players.split(','):
        tag = f'{size}-{players}p'
        log = Path(f'/tmp/td-view-{tag}.log')
        if log.exists(): log.unlink()
        command = [args.app, '--mute-audio', '--integration-view', '--view-size', size, '--view-players', players,
                   '-screen-width', width, '-screen-height', height, '-screen-fullscreen', '0', '-logFile', str(log)]
        command = [command[0], '-batchmode'] + ([] if args.screenshots else ['-nographics']) + command[1:]
        if args.screenshots: command += ['--view-screenshots', '--view-shots', args.shots]
        process = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 180
        while process.poll() is None:
            text = log.read_text(errors='replace') if log.exists() else ''
            if 'Exception: ' in text or time.monotonic() > deadline:
                process.terminate(); break
            time.sleep(1)
        text = log.read_text(errors='replace') if log.exists() else ''
        lines = [l for l in text.splitlines() if l.startswith('VIEW ') or 'Exception: ' in l or l.startswith('[Notice]') or 'Presentation exceeded' in l]
        evidence += [f'## {tag}'] + lines
        ok = f'VIEW {tag} PASS ALL' in text
        print(('PASS ' if ok else 'FAIL ') + tag + ('' if ok else ': ' + next((l for l in lines if 'Exception: ' in l), 'no PASS ALL')))
        if not ok: failed.append(tag)
name = 'observed-view-screenshots.log' if args.screenshots else 'observed-view.log'
Path(__file__).with_name(name).write_text('\n'.join(evidence) + '\n')
if failed: raise SystemExit('FAILED: ' + ', '.join(failed))
print('PASS view probes: every size and player count')
