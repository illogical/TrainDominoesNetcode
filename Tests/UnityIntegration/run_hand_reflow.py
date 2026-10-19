#!/usr/bin/env python3
"""Actual-window hand resize probe. Requires isolated HandReflowRuntime player; no build/network/offscreen fallback."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import struct
import tempfile
import time


def encode(run):
    rows = [line.split(',') for line in (run / 'frames.csv').read_text().splitlines()]
    if len(rows) < 2:
        raise RuntimeError('Insufficient actual frames for clip')
    entries = []
    for index, (number, timestamp) in enumerate(rows):
        duration = float(rows[index + 1][1]) - float(timestamp) if index + 1 < len(rows) else .05
        if duration <= 0:
            raise RuntimeError('Nonmonotonic real capture timestamps')
        if not (run / 'frames' / f'{int(number):06d}.png').is_file():
            raise RuntimeError(f'Missing actual frame {number}')
        entries += [f"file 'frames/{int(number):06d}.png'", f'duration {duration:.6f}']
    entries.append(f"file 'frames/{int(rows[-1][0]):06d}.png'")
    (run / 'concat.txt').write_text('\n'.join(entries) + '\n')
    # Window size changes across frames. Pad to a constant canvas without resizing tile pixels.
    dimensions = []
    for number, _ in rows:
        with (run / 'frames' / f'{int(number):06d}.png').open('rb') as source:
            header = source.read(24)
        if len(header) != 24 or header[:8] != b'\x89PNG\r\n\x1a\n':
            raise RuntimeError(f'Incomplete/non-PNG actual frame {number}')
        dimensions.append(struct.unpack('>II', header[16:24]))
    width = max(w for w, _ in dimensions); height = max(h for _, h in dimensions)
    width += width % 2; height += height % 2
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0',
                    '-i', str(run / 'concat.txt'), '-vf', f'pad={width}:{height}:0:0:black',
                    '-fps_mode', 'vfr', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(run / 'resize.mp4')], check=True)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--app', type=Path, default=Path('/tmp/td-integration-build/Integration.app/Contents/MacOS/TrainDominoesNetcode'))
    p.add_argument('--sizes', default='1280x720,1920x1080,1280x720')
    p.add_argument('--count', type=int, default=16, help='Synthetic local hand size, 1–74')
    p.add_argument('--reduced-motion', action='store_true', help='Use production short nonbouncing reflow')
    p.add_argument('--exercise-interruption', action='store_true', help='Simulate held navigator ownership during third resize; requires a layout-changing sequence')
    p.add_argument('--output', type=Path, help='New or empty directory, default temporary directory')
    p.add_argument('--no-clip', action='store_true', help='Keep PNG frames and timestamps without encoding')
    args = p.parse_args()
    sizes = args.sizes.split(',')
    if args.exercise_interruption and len(sizes) < 3:
        p.error('--exercise-interruption requires at least three --sizes entries')
    if not 1 <= args.count <= 74:
        p.error('--count must be 1–74')
    try:
        for size in sizes:
            w, h = map(int, size.split('x'))
            if w < 320 or h < 320:
                raise ValueError('dimensions must be at least 320')
    except ValueError as error:
        p.error(f'Invalid --sizes: {error}')
    if not args.app.is_file():
        p.error('Missing isolated HandReflowRuntime-equipped player executable')
    if not args.no_clip and not shutil.which('ffmpeg'):
        p.error('ffmpeg required; use --no-clip to retain frame PNGs only')
    run = args.output.resolve() if args.output else Path(tempfile.mkdtemp(prefix='td-hand-reflow-'))
    if args.output:
        run.mkdir(parents=True, exist_ok=True)
        if any(run.iterdir()):
            p.error('--output must be empty to preserve evidence')
    print(f'Actual-window hand reflow evidence: {run}', flush=True)
    (run / 'manifest.json').write_text(json.dumps(dict(
        scenario='synthetic-recipient-real-window-resize', requestedSizes=sizes, handCount=args.count, reducedMotion=args.reduced_motion, simulatedHeldGestureInterruption=args.exercise_interruption, recipientRevision=1,
        executable=str(args.app.resolve()), executableSha256=hashlib.sha256(args.app.read_bytes()).hexdigest(),
        humanVisualAcceptance='UNREVIEWED', realTouchAndTrackpad='UNREVIEWED', network='not exercised',
        timeScale=1, clipTiming='actual realtime timestamps; constant padded canvas preserves original tile pixels',
        startedUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())), indent=2) + '\n')
    width, height = sizes[0].split('x')
    command = [str(args.app), '--integration-hand-reflow', '--mute-audio', '--hand-reflow-dir', str(run),
               '--hand-reflow-sizes', args.sizes, '--hand-reflow-count', str(args.count), '-screen-fullscreen', '0', '-screen-width', width,
               '-screen-height', height, '-logFile', str(run / 'player.log')]
    if args.reduced_motion:
        command.append('--reduced-motion')
    if args.exercise_interruption:
        command.append('--hand-reflow-interrupt')
    (run / 'command.json').write_text(json.dumps(command, indent=2) + '\n')
    process = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
    ok = False
    try:
        deadline = time.monotonic() + 180
        while process.poll() is None:
            log = (run / 'player.log').read_text(errors='replace') if (run / 'player.log').exists() else ''
            if 'Exception:' in log or time.monotonic() > deadline:
                break
            time.sleep(.2)
        log = (run / 'player.log').read_text(errors='replace') if (run / 'player.log').exists() else ''
        ok = 'HAND_REFLOW PASS ALL' in log and (run / 'complete').exists()
        lines = [line for line in log.splitlines() if line.startswith('HAND_REFLOW ') or 'Exception:' in line]
        (run / 'receipt.log').write_text('\n'.join(lines) + '\n')
        if not ok:
            raise RuntimeError(f'Actual-window probe failed; inspect {run / "receipt.log"}')
        if not args.no_clip:
            encode(run)
        print(f'PASS actual-window automated probe; human visual acceptance UNREVIEWED. {run / "index.md"}')
    finally:
        if process.poll() is None:
            process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill(); process.wait()
        gallery = ['# Hand reflow actual-window evidence', '', f'Automated probe: **{"PASS" if ok else "FAILED / INCOMPLETE"}**.',
                   'Human visual acceptance: **UNREVIEWED**.', '',
                   f'Synthetic {args.count}-tile actor recipient view, one initial Receive, unchanged revision throughout real desktop client resizing. '
                   'No network, offscreen substitution, touch-device or manual window-drag claim.', '',
                   '[Manifest](manifest.json) · [Runtime receipts](receipt.log) · [Every-frame samples](samples.jsonl)', '']
        for path in sorted(run.glob('[0-9]*.png')):
            gallery.append(f'- [{path.stem}]({path.name}) · [metadata]({path.with_suffix(".json").name})')
        if (run / 'resize.mp4').exists():
            gallery += ['', '[Normal-speed clip](resize.mp4). PNG source frames and realtime timestamps are retained. '
                        'Variable client sizes are padded to a common canvas; native UI/text pixels are not scaled.']
        (run / 'index.md').write_text('\n'.join(gallery) + '\n')


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, subprocess.CalledProcessError, KeyboardInterrupt) as error:
        raise SystemExit(str(error))
