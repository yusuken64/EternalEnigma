"""Deterministically author original, short retro cues. No external samples or dependencies."""
from pathlib import Path
import math
import random
import struct
import wave

RATE = 44100
DEST = Path(__file__).resolve().parents[1] / 'Assets/Sounds/ActionFeedback'
DEST.mkdir(parents=True, exist_ok=True)

def tone(frequency, duration, amplitude=.4, bell=False, end=None):
    result = []
    phase = 0.0
    count = int(duration * RATE)
    for i in range(count):
        t = i / RATE
        phase += 2 * math.pi * (frequency if end is None else frequency * (end / frequency) ** (i / count)) / RATE
        attack = min(1., t / .004)
        tail = min(1., (duration - t) / .018)
        envelope = attack * tail * math.exp(-t * (9 if bell else 3))
        # Soft harmonics retain a cartoony retro edge without harsh square-wave aliasing.
        sample = math.sin(phase) + (.22 * math.sin(phase * 2.76) if bell else .18 * math.sin(phase * 3))
        result.append(amplitude * envelope * sample)
    return result

def sequence(notes, step=.065, tail=.18, bell=False):
    out = [0.] * int((step * (len(notes)-1) + tail) * RATE + 2)
    for n, frequency in enumerate(notes):
        part = tone(frequency, tail, .32, bell)
        offset = int(n * step * RATE)
        for i, v in enumerate(part): out[offset+i] += v
    return out

def save(name, data):
    peak = max(abs(v) for v in data) or 1
    # A conservative peak leaves headroom for simultaneous combat/UI sounds.
    scale = min(1., .62 / peak)
    with wave.open(str(DEST / (name + '.wav')), 'wb') as out:
        out.setnchannels(1); out.setsampwidth(2); out.setframerate(RATE)
        out.writeframes(b''.join(struct.pack('<h', round(v * scale * 32767)) for v in data))

save('GateUnlock', sequence([262,294,330,349,392,440,494,523,587,659,698,784,880,988,1047], .028, .17))
save('LevelUp', sequence([1047,1568], .09, .30, True))
save('Purchase', sequence([784,1047,1319], .055, .16, True))
save('Sell', sequence([1319,988,784], .05, .16, True))
save('Wait', tone(440, .045, .2))
save('Rest', sequence([523,392], .08, .16))
save('CastStart', tone(330, .16, .28, end=660))
save('Status', tone(660, .095, .22, end=880))
# A tiny latch click followed by a rubbery rising wooden creak.
rng = random.Random(7)
door = tone(150, .27, .32, end=280)
for i in range(int(.025 * RATE)):
    door[i] += rng.uniform(-1,1) * .25 * math.exp(-i / (RATE * .005))
save('DoorOpen', door)
print('Authored 9 original mono PCM cues in', DEST)
