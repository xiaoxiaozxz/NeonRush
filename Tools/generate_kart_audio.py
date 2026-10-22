"""Deterministic original racing SFX. Python standard library only; no external samples.

Run from any folder with: python Tools/generate_kart_audio.py
Writes mono 44.1 kHz / 16-bit PCM WAVs, a preview, and measurable QC results.
"""
from pathlib import Path
from array import array
import json
import math
import random
import wave

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "NeonRush/Assets/_Project/Audio"
CAPTURES = ROOT / "NeonRush/Captures"
RATE = 44100
TAU = math.tau
CLIPS = {}
REPORT = {}


def save(name, values, loop=False, peak=0.72):
    mean = sum(values) / len(values)
    values = [v - mean for v in values]
    largest = max(abs(v) for v in values) or 1
    values = [v * peak / largest for v in values]
    if not loop:
        fade = min(int(RATE * 0.006), len(values) // 4)
        for i in range(fade):
            gain = math.sin(i / fade * math.pi / 2) ** 2
            values[i] *= gain
            values[-1-i] *= gain
    pcm = array("h", [round(max(-0.999, min(0.999, v)) * 32767) for v in values])
    with wave.open(str(OUT / (name + ".wav")), "wb") as handle:
        handle.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        handle.writeframes(pcm.tobytes())
    CLIPS[name] = values
    REPORT[name] = dict(seconds=round(len(values)/RATE, 3), peak=round(max(abs(v) for v in values), 4),
                        rms=round(math.sqrt(sum(v*v for v in values)/len(values)), 4), loop=loop,
                        boundary_step=round(abs(values[-1]-values[0]), 5),
                        largest_sample_step=round(max(abs(values[i]-values[i-1]) for i in range(1, len(values))), 5),
                        boundary_slope_error=round(abs((values[0]-values[-1])-(values[1]-values[0])), 5),
                        dc=round(sum(values)/len(values), 7))


def periodic_noise(seconds, seed, count=70, low=150, high=4200):
    rng = random.Random(seed)
    tones = [(round(rng.uniform(low, high)*seconds)/seconds, rng.uniform(0, TAU),
              rng.uniform(0.4, 1.0)) for _ in range(count)]
    values = [0.0] * round(RATE * seconds)
    for freq, phase, amp in tones:
        step = TAU * freq / RATE
        # Recurrence avoids millions of expensive trigonometric calls.
        sine, cosine = math.sin(phase), math.cos(phase)
        ss, cc = math.sin(step), math.cos(step)
        for i in range(len(values)):
            values[i] += sine * amp / math.sqrt(count)
            sine, cosine = sine * cc + cosine * ss, cosine * cc - sine * ss
    return values


def engine(name, base, seed):
    seconds = 2.0
    texture = periodic_noise(seconds, seed, count=24, low=120, high=1500)
    values = []
    for i, grain in enumerate(texture):
        t = i / RATE
        pulse = sum(math.sin(TAU*base*h*t) / h**1.2 for h in range(1, 8))
        rumble = 0.3*math.sin(TAU*base/2*t)
        modulation = 0.94 + 0.06*math.sin(TAU*4*t)
        values.append(math.tanh((pulse*0.7 + rumble + grain*0.035)*1.5)*modulation)
    save(name, values, loop=True, peak=0.62)


def impact(name, seed, duration=0.38, bass=125):
    rng = random.Random(seed)
    low = 0
    values = []
    for i in range(round(RATE*duration)):
        t = i/RATE
        noise = rng.uniform(-1, 1)
        low += (noise-low)*0.14
        tonal = math.sin(TAU*(bass*t-55*t*t))*math.exp(-17*t)
        values.append(tonal*0.65 + low*2*math.exp(-25*t) + noise*0.14*math.exp(-55*t))
    save(name, values)


def sweep(name, duration, seed, base, rise, strength=0.75):
    rng = random.Random(seed)
    low, phase = 0, 0
    values = []
    for i in range(round(RATE*duration)):
        t = i/RATE
        u = t/duration
        low += (rng.uniform(-1, 1)-low)*0.32
        phase += TAU*(base + rise*u)/RATE
        envelope = math.sin(math.pi*u)**0.75 * math.exp(-0.6*u)
        body = math.sin(phase)*0.28 + math.sin(phase*0.5)*0.15
        values.append((low*strength + body)*envelope)
    save(name, values)


def chime(name, notes, step=0.11, tail=0.3):
    values = [0.0]*round(RATE*(step*len(notes)+tail))
    for j, freq in enumerate(notes):
        start = round(j*step*RATE)
        for i in range(len(values)-start):
            t = i/RATE
            env = min(1, t/0.005)*math.exp(-t/0.12)
            values[start+i] += (math.sin(TAU*freq*t) + 0.2*math.sin(TAU*freq*2*t))*env
    save(name, values, peak=0.6)


def preview():
    mix = [0.0] * (RATE * 17)
    def place(name, at, gain=0.7, duration=None, pitch=1.0):
        source = CLIPS[name]
        count = round((duration or len(source)/RATE)*RATE)
        start = round(at*RATE)
        for i in range(count):
            if start+i >= len(mix): break
            position = i*pitch
            k = int(position) % len(source)
            fraction = position-int(position)
            value = source[k]*(1-fraction)+source[(k+1)%len(source)]*fraction
            env = min(1, i/(RATE*0.025), (count-i)/(RATE*0.08))
            mix[start+i] += value*gain*env
    place("engine_idle_loop", 0, 0.35, 2)
    for j in range(4): place("engine_drive_loop", 2+j*0.8, 0.45, 0.88, 0.8+j*0.3)
    place("tyre_skid_loop", 5.5, 0.38, 1.8)
    place("collision_01", 7.5, 0.65)
    place("reset", 8.2, 0.55)
    place("gas_ready", 9.0, 0.6)
    place("nitro_burst", 10.1, 0.7)
    place("mini_boost", 11.7, 0.7)
    place("air_boost", 12.6, 0.7)
    place("landing", 13.5, 0.65)
    place("countdown_tick", 14.2, 0.55)
    place("countdown_tick", 14.8, 0.55)
    place("race_start", 15.4, 0.65)
    maximum = max(abs(v) for v in mix)
    gain = min(1, 0.85/maximum)
    pcm = array("h", [round(v*gain*32767) for v in mix])
    with wave.open(str(CAPTURES / "racing-sfx-preview.wav"), "wb") as handle:
        handle.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        handle.writeframes(pcm.tobytes())


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    CAPTURES.mkdir(parents=True, exist_ok=True)
    engine("engine_idle_loop", 60, 101)
    engine("engine_drive_loop", 90, 102)
    hiss = periodic_noise(2, 103, low=450, high=5500)
    save("wind_loop", hiss, loop=True, peak=0.55)
    skid = periodic_noise(2, 104, low=350, high=4100)
    save("tyre_skid_loop", [(value*0.75 + 0.13*math.sin(TAU*940*i/RATE)) *
                            (0.9+0.1*math.sin(TAU*7*i/RATE)) for i, value in enumerate(skid)], loop=True, peak=0.62)
    for index in range(3): impact("collision_0"+str(index+1), 200+index, bass=110+index*19)
    impact("landing", 220, duration=0.42, bass=80)
    sweep("nitro_burst", 1.25, 301, 140, 620, 1.3)
    sweep("mini_boost", 0.42, 302, 240, 900, 0.9)
    sweep("air_boost", 0.55, 303, 420, 1100, 1.1)
    chime("gas_ready", [659.25, 830.61, 987.77], step=0.085)
    chime("reset", [880, 659.25, 523.25], step=0.07)
    chime("ui_pause", [440, 329.63], step=0.05, tail=0.15)
    chime("ui_resume", [523.25, 659.25], step=0.05, tail=0.15)
    chime("countdown_tick", [740], tail=0.11)
    chime("race_start", [740, 987.77, 1480], step=0.065)
    preview()
    for name, result in REPORT.items():
        assert result["peak"] < 0.9, (name, "clipping headroom")
        assert result["rms"] > 0.02, (name, "silent clip")
        assert abs(result["dc"]) < 0.005, (name, "DC offset")
        if result["loop"]:
            assert result["boundary_step"] <= result["largest_sample_step"]*1.02+0.0001, (name, "loop discontinuity")
            assert result["boundary_slope_error"] < 0.025, (name, "loop slope discontinuity")
    report_path = CAPTURES / "audio-quality-report.json"
    report_path.write_text(json.dumps(REPORT, indent=2), encoding="utf-8")
    print(f"Generated {len(CLIPS)} WAV clips. All peak, RMS, DC and loop-boundary checks passed.")
    print(f"Preview: {CAPTURES / 'racing-sfx-preview.wav'}")


if __name__ == "__main__": main()
