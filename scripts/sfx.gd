extends Node
## Procedural sound: every effect is synthesized at startup from oscillators and filtered noise,
## so the game ships with no audio files. Play with Sfx.play("name") or Sfx.play_at("name", pos).

const RATE := 22050
const POOL_2D := 10
const POOL_3D := 16

var _streams := {}
var _pool_2d: Array[AudioStreamPlayer] = []
var _pool_3d: Array[AudioStreamPlayer3D] = []
var _next_2d := 0
var _next_3d := 0
var _ambient: AudioStreamPlayer
var _gull_timer := 6.0
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_rng.seed = 1234
	_streams = {
		"splash": _splash(0.55, 7.0, 0.7),
		"splash_big": _splash(1.1, 3.5, 1.0),
		"plop": _plop(),
		"whoosh": _whoosh(),
		"tick": _tick(),
		"snap": _snap(),
		"jingle": _arpeggio([523.3, 659.3, 784.0, 1046.5], 0.085, 0.45),
		"golden": _arpeggio([784.0, 987.8, 1174.7, 1568.0, 1975.5, 2349.3], 0.06, 0.4),
		"escape": _arpeggio([392.0, 349.2, 293.7], 0.12, 0.35),
		"cash": _cash(),
		"blip": _tone(880.0, 0.07, 0.35, "sine"),
		"deny": _tone(140.0, 0.16, 0.25, "square"),
		"horn": _horn(),
		"step_wood": _step(true),
		"step_sand": _step(false),
		"jump": _tone(330.0, 0.08, 0.15, "sine", 1.6),
		"gull": _gull(),
		"waves": _waves(),
	}
	for i in range(POOL_2D):
		var p := AudioStreamPlayer.new()
		add_child(p)
		_pool_2d.append(p)
	for i in range(POOL_3D):
		var p := AudioStreamPlayer3D.new()
		p.unit_size = 6.0
		p.max_distance = 60.0
		add_child(p)
		_pool_3d.append(p)
	_ambient = AudioStreamPlayer.new()
	_ambient.stream = _streams["waves"]
	_ambient.volume_db = -13.0
	add_child(_ambient)
	_ambient.play()


## Stop everything, give the audio server a few frames to drop its playbacks, then quit.
## Quitting mid-sound otherwise leaves playback objects alive at exit.
func quit_cleanly(code := 0) -> void:
	_release()
	for i in range(4):
		await get_tree().process_frame
	get_tree().quit(code)


func _exit_tree() -> void:
	_release()


func _release() -> void:
	set_process(false)
	for p in _pool_2d:
		p.stop()
		p.stream = null
	for p in _pool_3d:
		p.stop()
		p.stream = null
	if _ambient:
		_ambient.stop()
		_ambient.stream = null
	_streams.clear()


func _process(delta: float) -> void:
	_gull_timer -= delta
	if _gull_timer <= 0.0:
		_gull_timer = _rng.randf_range(8.0, 20.0)
		play("gull", _rng.randf_range(0.85, 1.2), -16.0)


func play(sound: String, pitch := 1.0, volume_db := 0.0) -> void:
	if not _streams.has(sound):
		push_warning("Unknown sound: " + sound)
		return
	var p := _pool_2d[_next_2d]
	_next_2d = (_next_2d + 1) % POOL_2D
	p.stream = _streams[sound]
	p.pitch_scale = pitch
	p.volume_db = volume_db
	p.play()


func play_at(sound: String, pos: Vector3, pitch := 1.0, volume_db := 0.0) -> void:
	if not _streams.has(sound):
		push_warning("Unknown sound: " + sound)
		return
	var p := _pool_3d[_next_3d]
	_next_3d = (_next_3d + 1) % POOL_3D
	p.stream = _streams[sound]
	p.global_position = pos
	p.pitch_scale = pitch
	p.volume_db = volume_db
	p.play()


func has_sound(sound: String) -> bool:
	return _streams.has(sound)


# ------------------------------------------------------------------ synthesis helpers

func _to_stream(samples: PackedFloat32Array, loop := false) -> AudioStreamWAV:
	var data := PackedByteArray()
	data.resize(samples.size() * 2)
	for i in range(samples.size()):
		data.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32000.0))
	var s := AudioStreamWAV.new()
	s.format = AudioStreamWAV.FORMAT_16_BITS
	s.mix_rate = RATE
	s.stereo = false
	s.data = data
	if loop:
		s.loop_mode = AudioStreamWAV.LOOP_FORWARD
		s.loop_begin = 0
		s.loop_end = samples.size()
	return s


func _buffer(seconds: float) -> PackedFloat32Array:
	var b := PackedFloat32Array()
	b.resize(int(seconds * RATE))
	return b


func _osc(kind: String, phase: float) -> float:
	match kind:
		"square":
			return 1.0 if fmod(phase, 1.0) < 0.5 else -1.0
		"saw":
			return fmod(phase, 1.0) * 2.0 - 1.0
		"tri":
			return absf(fmod(phase, 1.0) * 4.0 - 2.0) - 1.0
	return sin(phase * TAU)


func _tone(freq: float, dur: float, vol: float, kind := "sine", sweep := 1.0) -> AudioStreamWAV:
	var b := _buffer(dur)
	var phase := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var f := freq * lerpf(1.0, sweep, t / dur)
		phase += f / RATE
		var env := minf(t / 0.005, 1.0) * exp(-t * 6.0 / dur)
		b[i] = _osc(kind, phase) * env * vol
	return _to_stream(b)


func _splash(dur: float, decay: float, vol: float) -> AudioStreamWAV:
	var b := _buffer(dur)
	var lp := 0.0
	var lp2 := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var n := _rng.randf_range(-1.0, 1.0)
		var cutoff := lerpf(0.55, 0.08, minf(t / dur, 1.0))
		lp += cutoff * (n - lp)
		lp2 += 0.02 * (n - lp2)
		var env := minf(t / 0.008, 1.0) * exp(-t * decay)
		# a few bubbly blips on top of the wash
		var bubble := sin(t * TAU * (300.0 + 900.0 * fmod(t * 13.0, 1.0))) * 0.15 * exp(-t * decay * 1.5)
		b[i] = ((lp - lp2 * 0.5) * 1.4 + bubble) * env * vol
	return _to_stream(b)


func _plop() -> AudioStreamWAV:
	var b := _buffer(0.18)
	var phase := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var f := lerpf(950.0, 220.0, minf(t / 0.12, 1.0))
		phase += f / RATE
		b[i] = sin(phase * TAU) * exp(-t * 22.0) * 0.6 + _rng.randf_range(-1, 1) * exp(-t * 60.0) * 0.08
	return _to_stream(b)


func _whoosh() -> AudioStreamWAV:
	var dur := 0.38
	var b := _buffer(dur)
	var lp1 := 0.0
	var lp2 := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var n := _rng.randf_range(-1.0, 1.0)
		var c := lerpf(0.05, 0.35, t / dur)
		lp1 += c * (n - lp1)
		lp2 += c * 0.3 * (n - lp2)
		b[i] = (lp1 - lp2) * sin(PI * t / dur) * 1.6
	return _to_stream(b)


func _tick() -> AudioStreamWAV:
	var b := _buffer(0.025)
	for i in range(b.size()):
		var t := float(i) / RATE
		b[i] = (_rng.randf_range(-1, 1) * 0.5 + sin(t * TAU * 2600.0) * 0.5) * exp(-t * 350.0) * 0.5
	return _to_stream(b)


func _snap() -> AudioStreamWAV:
	var b := _buffer(0.35)
	var prev := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var n := _rng.randf_range(-1.0, 1.0)
		var hp := n - prev
		prev = n
		b[i] = hp * exp(-t * 45.0) * 0.8 + sin(t * TAU * 95.0) * exp(-t * 14.0) * 0.5
	return _to_stream(b)


func _arpeggio(notes: Array, step: float, vol: float) -> AudioStreamWAV:
	var dur := step * notes.size() + 0.45
	var b := _buffer(dur)
	for n in range(notes.size()):
		var f: float = notes[n]
		var start := int(n * step * RATE)
		for i in range(start, b.size()):
			var t := float(i - start) / RATE
			var env := minf(t / 0.004, 1.0) * exp(-t * 7.0)
			b[i] += (sin(t * TAU * f) + 0.3 * sin(t * TAU * f * 2.0) + 0.1 * sin(t * TAU * f * 3.0)) * env * vol * 0.6
	return _to_stream(b)


func _cash() -> AudioStreamWAV:
	var b := _buffer(0.8)
	for i in range(b.size()):
		var t := float(i) / RATE
		var click := _rng.randf_range(-1, 1) * exp(-t * 90.0) * 0.5
		var t2 := maxf(t - 0.06, 0.0)
		var bell := 0.0
		if t > 0.06:
			bell = (sin(t2 * TAU * 1318.5) + sin(t2 * TAU * 1760.0) * 0.8 + sin(t2 * TAU * 2637.0) * 0.3) * exp(-t2 * 5.5) * 0.28
		b[i] = click + bell
	return _to_stream(b)


func _horn() -> AudioStreamWAV:
	var dur := 1.4
	var b := _buffer(dur)
	var p1 := 0.0
	var p2 := 0.0
	var lp := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		p1 += 110.0 / RATE
		p2 += 165.0 / RATE
		var raw := _osc("saw", p1) * 0.6 + _osc("saw", p2) * 0.4
		lp += 0.08 * (raw - lp)
		var env := minf(t / 0.15, 1.0) * minf((dur - t) / 0.35, 1.0)
		b[i] = lp * env * 0.7
	return _to_stream(b)


func _step(wood: bool) -> AudioStreamWAV:
	var b := _buffer(0.09)
	var lp := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var n := _rng.randf_range(-1.0, 1.0)
		lp += (0.25 if wood else 0.12) * (n - lp)
		var body := sin(t * TAU * 150.0) * exp(-t * 55.0) * 0.5 if wood else 0.0
		b[i] = (lp * (0.6 if wood else 0.9) + body) * exp(-t * (40.0 if wood else 28.0)) * 0.6
	return _to_stream(b)


func _gull() -> AudioStreamWAV:
	var dur := 0.65
	var b := _buffer(dur)
	var phase := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var u := t / dur
		# "kee-ow": rise then fall, with a throaty vibrato
		var f := 1100.0 + 500.0 * sin(u * PI) - 350.0 * u + 40.0 * sin(t * TAU * 28.0)
		phase += f / RATE
		var env := sin(PI * minf(u * 1.3, 1.0)) * (1.0 - u * 0.4)
		b[i] = (_osc("saw", phase) * 0.35 + sin(phase * TAU) * 0.5) * env * 0.35
	return _to_stream(b)


func _waves() -> AudioStreamWAV:
	# 8 s of brown-ish noise with slow swells; the tail is cross-faded into the head so it loops cleanly
	var dur := 8.0
	var fade := int(1.0 * RATE)
	var b := _buffer(dur + 1.0)
	var lp := 0.0
	var lp2 := 0.0
	for i in range(b.size()):
		var t := float(i) / RATE
		var n := _rng.randf_range(-1.0, 1.0)
		lp += 0.06 * (n - lp)
		lp2 += 0.004 * (n - lp2)
		var swell := 0.45 + 0.35 * sin(t * TAU / 4.0) + 0.2 * sin(t * TAU / 2.67 + 1.0)
		b[i] = (lp * 0.9 + lp2 * 3.0) * swell * 0.8
	var out := _buffer(dur)
	for i in range(out.size()):
		out[i] = b[i]
	for i in range(fade):
		var w := float(i) / fade
		out[i] = b[i] * w + b[out.size() + i] * (1.0 - w)
	return _to_stream(out, true)
