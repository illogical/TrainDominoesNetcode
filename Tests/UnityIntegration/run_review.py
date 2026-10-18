#!/usr/bin/env python3
"""Visible two-peer localhost review, actual-frame PNG/MP4 gallery, guided checkpoints.

Never writes evidence into tracked Assets or existing regression logs. Needs macOS,
ffmpeg, swift (built-in Vision OCR), and a ReviewRuntime-equipped temporary player.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time

HERE = Path(__file__).resolve().parent
ROLES = ('host', 'client')


def source_identity():
    files = sorted((HERE.parents[1] / 'Assets' / 'Scripts').rglob('*.cs'))
    digest = hashlib.sha256()
    for path in files + sorted(HERE.glob('*.cs')):
        digest.update(str(path.relative_to(HERE.parents[1])).encode())
        digest.update(path.read_bytes())
    return digest.hexdigest()


def check_logs(run):
    for role in ROLES:
        path = run / f'{role}.log'
        if path.exists():
            text = path.read_text(errors='replace')
            if re.search(r'(?:Exception: (?:ASSERT|TIMEOUT)|error CS\d+|NullReferenceException)', text):
                raise RuntimeError(f'{role} runtime failed; inspect {path}')


def wait_for(test, run, peers, description, limit=50):
    deadline = time.monotonic() + limit
    while not test():
        check_logs(run)
        if any(peer.poll() is not None for peer in peers):
            raise RuntimeError(f'Player exited before {description}; inspect logs')
        if time.monotonic() > deadline:
            raise RuntimeError(f'Timed out awaiting {description}')
        time.sleep(.15)


def ocr_check(run, images):
    """Require a rasterized proof label AND at least one matching production UI label."""
    result = subprocess.run(['swift', '-module-cache-path', str(run / 'swift-cache'), str(HERE / 'review_ocr.swift'), *map(str, images)],
                            capture_output=True, text=True, check=True)
    entries = [json.loads(line) for line in result.stdout.splitlines() if line.startswith('{')]
    if len(entries) != len(images):
        raise RuntimeError('OCR did not return every checkpoint image')
    checks = []
    normalize = lambda text: re.sub('[^a-z0-9]', '', text.lower())
    for entry in entries:
        image = Path(entry['file'])
        metadata = json.loads(image.with_suffix('.json').read_text())
        actual = normalize(entry['text'])
        # Ignore single glyph labels and rich text tags. Require readable text which
        # actually belongs to production UI; the runner's stamp alone cannot pass.
        candidates = [normalize(re.sub('<[^>]+>', '', label)) for label in metadata['uiText']]
        matches = [label for label in candidates if len(label) >= 5 and label in actual]
        passed = 'reviewframetext' in actual and bool(matches)
        checks.append(dict(file=image.name, passed=passed, matchedProductionLabels=matches,
                           recognizedText=entry['text']))
    (run / 'capture-checks.json').write_text(json.dumps(checks, indent=2) + '\n')
    return all(check['passed'] for check in checks)


def motion_check(run):
    checks = []
    for role in ROLES:
        samples = [json.loads(line) for line in (run / f'{role}-motion.jsonl').read_text().splitlines()]
        changed = 0
        for before, after in zip(samples, samples[1:]):
            if not (before['busy'] and after['busy']):
                continue
            previous = {tile['id']: tile for tile in before['tiles']}
            for tile in after['tiles']:
                old = previous.get(tile['id'])
                if old and any(abs(tile['position'][axis] - old['position'][axis]) > .001 for axis in ('x', 'y', 'z')):
                    changed += 1
                    break
        real = samples[-1]['realTime'] - samples[0]['realTime'] if samples else 0
        scaled = samples[-1]['scaledTime'] - samples[0]['scaledTime'] if samples else 0
        ratio = scaled / real if real else 0
        checks.append(dict(role=role, passed=changed >= 2 and real >= .5 and .85 <= ratio <= 1.15,
                           movingFramePairs=changed, elapsedUnpausedRealtime=real,
                           elapsedUnpausedScaledTime=scaled, timeRatio=ratio))
    (run / 'motion-checks.json').write_text(json.dumps(checks, indent=2) + '\n')
    return all(check['passed'] for check in checks)


def encode(run, role):
    frames = run / f'{role}-frames'
    timings = [(int(a), float(b)) for a, b in
               (line.split(',') for line in (run / f'{role}-frames.csv').read_text().splitlines())]
    if len(timings) < 2:
        raise RuntimeError(f'{role} has insufficient actual animation frames')
    concat = run / f'{role}-concat.txt'
    lines = []
    for index, (number, timestamp) in enumerate(timings):
        lines.append(f"file '{role}-frames/{number:06d}.png'")
        duration = timings[index + 1][1] - timestamp if index + 1 < len(timings) else .1
        if duration <= 0:
            raise RuntimeError('Non-monotonic normal-speed capture timestamps')
        lines.append(f'duration {duration:.6f}')
    lines.append(f"file '{role}-frames/{timings[-1][0]:06d}.png'")
    concat.write_text('\n'.join(lines) + '\n')
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat',
                    '-safe', '0', '-i', str(concat), '-fps_mode', 'vfr', '-c:v', 'libx264',
                    '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(run / f'{role}.mp4')], check=True)


def gallery(run, capture_pass, status):
    text = ['# Actual localhost gameplay review', '', f'Automated scenario: **{status}**.',
            f'Actual-frame capture checks: **{"PASS" if capture_pass else "FAILED / UNVERIFIED"}**.',
            'Human visual acceptance: **UNREVIEWED**. No real input/device or live-service claim.', '',
            'Deterministic ordered deck (`IntegrationRuntime.Deck`, round 0); two actual localhost peers. '
            'Production state/client/authority/presenter; fixture authority injected. Match id `cli-review-fixture`. '
            'Normal startup is covered separately by `run_players.py --normal`.', '',
            'Clips preserve actual frame timestamps at Time.timeScale=1. Intentional checkpoint inspection pauses '
            'are excluded. PNGs capture the real game window with production UI text. OCR checks both the '
            'frame stamp and a production UI label; inspect remaining text for readability.', '',
            '[Build/source identity](manifest.json) · [Capture checks](capture-checks.json) · '
            '[Expectations](EXPECTATIONS.md) · [Motion checks](motion-checks.json)', '',
            '[Host original clip](host.mp4) · [Client original clip](client.mp4)', '',
            '| Checkpoint | Host | Client | Evidence |', '| --- | --- | --- | --- |']
    for path in sorted(run.glob('*-host.json')):
        base = path.name.removesuffix('-host.json')
        text.append(f'| {base} | [PNG]({base}-host.png) | [PNG]({base}-client.png) | '
                    f'[Host]({base}-host.json) / [Client]({base}-client.json) |')
    if (run / 'comparison.mp4').exists():
        text += ['', '[Optional side-by-side clip](comparison.mp4) (independent peer capture clocks; use checkpoint PNGs for exact paired positions).']
    text += ['', 'The gallery contains both private recipient views for developer review. '
             'A paired gallery does not grant either game peer access to the other hand. '
             'Baseline checks cover own hand count and foreign provisional placements; no exhaustive packet privacy claim.', '']
    (run / 'index.md').write_text('\n'.join(text))


def main(output_override=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=Path('/tmp/td-integration-build/Integration.app/Contents/MacOS/TrainDominoesNetcode'))
    parser.add_argument('--output', type=Path, help='New/empty directory; default printed temporary directory')
    parser.add_argument('--guided', action='store_true')
    parser.add_argument('--actions', type=int, default=8)
    parser.add_argument('--width', type=int, default=1280)
    parser.add_argument('--height', type=int, default=720)
    parser.add_argument('--side-by-side', action='store_true', help='Also encode an optional host/client comparison; originals are retained')
    parser.add_argument('--keep-frames', action='store_true', help='Retain original PNG animation frames after successful encoding')
    args = parser.parse_args()
    if output_override:
        args.output = Path(output_override)
    if not args.app.is_file():
        parser.error('Build the isolated ReviewRuntime player first: app executable is missing')
    if args.actions < 1 or args.actions > 100 or args.width < 640 or args.height < 480:
        parser.error('actions must be 1–100; dimensions at least 640×480')
    for required in ('ffmpeg', 'swift'):
        if not shutil.which(required):
            parser.error(f'{required} is required for capture encoding/OCR validation')
    run = args.output.resolve() if args.output else Path(tempfile.mkdtemp(prefix='td-review-'))
    if args.output:
        run.mkdir(parents=True, exist_ok=True)
        if any(run.iterdir()):
            parser.error('--output must be empty to preserve prior review evidence')
    print(f'Review output: {run}', flush=True)
    expectations = (HERE / 'REVIEW_EXPECTATIONS.md').read_text()
    expectations = expectations.replace('](../../Assets/', f']({HERE.parents[1]}/Assets/')
    (run / 'EXPECTATIONS.md').write_text(expectations)
    manifest = dict(scenario='baseline-two-peer', fixture='IntegrationRuntime.Deck round 0, ordered deterministic deck; no seed',
                    mode='guided' if args.guided else 'automatic', resolution=[args.width, args.height],
                    executable=str(args.app.resolve()), executableSha256=hashlib.sha256(args.app.read_bytes()).hexdigest(),
                    currentSourceSha256=source_identity(), sourceIdentityScope='current workspace sources; not proof executable was built from these sources',
                    timeScale=1, peers=list(ROLES), acceptance='unreviewed', startedUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()))
    (run / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    peers = []
    status = 'FAILED / INCOMPLETE'
    capture_pass = False
    try:
        for role in ROLES:
            command = [str(args.app), '--integration-review', '--mute-audio', '--review-dir', str(run),
                       '--review-actions', str(args.actions), '-screen-fullscreen', '0', '-screen-width',
                       str(args.width), '-screen-height', str(args.height), '-logFile', str(run / f'{role}.log')]
            if role == 'host':
                command.append('--integration-host')
            peers.append(subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT))
            time.sleep(1)
        checkpoints = ['readiness', 'deal']
        for number in range(args.actions):
            checkpoints += [f'before-action-{number}', f'after-action-{number}']
        checkpoints += ['finished']
        for index, name in enumerate(checkpoints):
            checkpoint = f'{index:03d}-{name}'
            wait_for(lambda: all((run / f'{checkpoint}-{r}-ready').exists() for r in ROLES),
                     run, peers, checkpoint)
            print(f'Checkpoint {checkpoint}: paired PNGs saved', flush=True)
            if args.guided:
                while True:
                    answer = input('[Enter] continue, [t] manual takeover, [q] stop: ').strip().lower()
                    if answer == 't':
                        print('Both scripts are paused. Use the game UI. Scripted assertions will not resume. '
                              'Restart creates a fresh output directory and deterministic scenario.', flush=True)
                        answer = input('[Enter/r] restart fresh deterministic run; [q] stop: ').strip().lower()
                        status = 'MANUAL TAKEOVER / SCRIPT NOT RESUMED'
                        return 2 if answer == 'q' else 3
                    if answer == 'q':
                        status = 'STOPPED / INCOMPLETE'
                        return 2
                    if not answer:
                        break
            (run / f'{checkpoint}-continue').write_text('continue\n')
        wait_for(lambda: all((run / f'{role}-complete').exists() for role in ROLES), run, peers, 'completion')
        if not motion_check(run):
            raise RuntimeError('Actual production tile motion did not advance at normal speed; inspect motion-checks.json')
        status = 'AUTOMATED PASS'
        for role in ROLES:
            encode(run, role)
        if args.side_by_side:
            subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                            '-i', str(run / 'host.mp4'), '-i', str(run / 'client.mp4'),
                            '-filter_complex', '[0:v][1:v]hstack=inputs=2:shortest=1[v]',
                            '-map', '[v]', '-c:v', 'libx264', '-pix_fmt', 'yuv420p',
                            str(run / 'comparison.mp4')], check=True)
        capture_pass = ocr_check(run, sorted(run.glob('*-host.png')) + sorted(run.glob('*-client.png')))
        if not capture_pass:
            raise RuntimeError('Missing/unreadable frame stamp or production UI label: capture check FAILED')
        if not args.keep_frames:
            for role in ROLES:
                shutil.rmtree(run / f'{role}-frames')
        print(f'Automated checks passed; visual quality UNREVIEWED. Gallery: {run / "index.md"}')
        return 0
    finally:
        for peer in peers:
            if peer.poll() is None:
                peer.terminate()
        for peer in peers:
            try:
                peer.wait(timeout=5)
            except subprocess.TimeoutExpired:
                peer.kill()
        gallery(run, capture_pass, status)


if __name__ == '__main__':
    try:
        result = main()
        while result == 3:
            restart = tempfile.mkdtemp(prefix="td-review-restart-")
            print(f"Restarting deterministic scenario from the beginning: {restart}", flush=True)
            result = main(restart)
        sys.exit(result)
    except (RuntimeError, subprocess.CalledProcessError, KeyboardInterrupt) as error:
        print(f'Review failed: {error}', file=sys.stderr)
        sys.exit(1)
