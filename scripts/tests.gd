extends Node
## Automated smoke tests. Each prints "[smoke] PASS"/"FAIL" and exits with code 0/1.
##   -- --smoke                core loop: cast, bite, fight, catch, sell, buy
##   -- --smoke-islands        ferry unlock + travel, island-exclusive catch tables, every fish model, every sound
##   -- --smoke-host           host a session and wait for a client to exchange presence + a catch event
##   -- --smoke-join=ADDRESS   join a host and do the same from the client side
## Add `--shots=C:\dir` (and run without --headless) to save screenshots during --smoke.

var main: Node3D
var _shots_dir := ""


func _ready() -> void:
	Game.persist = false
	Game.reset_state()
	var args := OS.get_cmdline_user_args()
	for a in args:
		if a.begins_with("--shots="):
			_shots_dir = a.trim_prefix("--shots=")
	await get_tree().process_frame
	main.player.automation = true
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	main.start_game()
	if "--smoke" in args:
		_finish(await _core_loop())
	elif "--smoke-islands" in args:
		_finish(await _islands())
	elif "--smoke-gallery" in args:
		_finish(await _gallery())
	elif "--smoke-host" in args:
		_finish(await _net_host())
	else:
		for a in args:
			if a.begins_with("--smoke-join="):
				_finish(await _net_join(a.trim_prefix("--smoke-join=")))
				return
		print("[smoke] unknown test flag")
		_finish(false)


func _finish(ok: bool) -> void:
	print("[smoke] %s" % ("PASS" if ok else "FAIL"))
	Net.leave()
	Sfx.quit_cleanly(0 if ok else 1)


func _frames(n: int) -> void:
	for i in range(n):
		await get_tree().physics_frame


func _shot(shot_name: String) -> void:
	if _shots_dir == "" or DisplayServer.get_name() == "headless":
		return
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(_shots_dir.path_join(shot_name + ".png"))


func _market() -> Stall:
	for s in get_tree().get_nodes_in_group("stalls"):
		var st := s as Stall
		if st.kind == "market" and World.island_at(st.global_position) == Game.current_island:
			return st
	return null


## Cast, wait for a bite, hook it and fight with a sensible reel-in-bursts strategy.
## Returns "caught", "lost" or "no_bite".
func _fish_once(shots := false) -> String:
	var f: Fishing = main.player.fishing
	await _frames(30)
	f.auto_reel = 1
	await _frames(45)
	if shots:
		await _shot("1_charging")
	f.auto_reel = 0
	var waited := 0
	while f.state != Fishing.State.BITE and waited < 900:
		await get_tree().physics_frame
		waited += 1
		if shots and waited == 100:
			await _shot("2_waiting")
	if f.state != Fishing.State.BITE:
		f.reset()
		return "no_bite"
	var before := Game.cooler.size()
	await _frames(10)
	f.auto_reel = 1
	await get_tree().physics_frame
	var frames := 0
	while f.state == Fishing.State.FIGHT:
		f.auto_reel = 1 if f.tension < 0.7 else 0
		await get_tree().physics_frame
		frames += 1
		if shots and frames == 90:
			await _shot("3_fight")
	f.auto_reel = 0
	if shots:
		await _frames(12)
		await _shot("4_caught")
	return "caught" if Game.cooler.size() > before else "lost"


func _core_loop() -> bool:
	var outcomes := {"caught": 0, "lost": 0, "no_bite": 0}
	var attempts := 0
	while Game.cooler.size() < 3 and attempts < 12:
		attempts += 1
		var r := await _fish_once(attempts == 1)
		outcomes[r] += 1
		if r == "caught":
			var c: Dictionary = Game.cooler[-1]
			print("[smoke] caught %s size=%.2f value=$%d" % [c["id"], c["size"], c["value"]])
	var sold := Game.cooler.size()
	var m := _market()
	if m:
		m.interact(main.player)
	print("[smoke] outcomes=%s sold=%d money=$%d" % [outcomes, sold, Game.money])
	var bought := Game.buy_bait(1)
	print("[smoke] bought worms=%s counts=%s" % [bought, Game.bait_counts])
	if _shots_dir != "":
		main.hud.open_shop()
		await _frames(5)
		await _shot("5_shop")
		main.hud.close_panel()
		main.hud.open_travel()
		await _frames(5)
		await _shot("6_ferry")
		main.hud.close_panel()
	return sold > 0 and m != null and Game.cooler.is_empty() and bought


func _islands() -> bool:
	var ok := true
	# every creature model and every sound builds
	for id in Data.CREATURES.keys():
		for golden in [false, true]:
			var model := FishModel.build(id, 1.0, golden)
			if model.get_child_count() == 0:
				print("[smoke] model %s is empty" % id)
				ok = false
			model.free()
	for s in ["splash", "splash_big", "plop", "whoosh", "tick", "snap", "jingle", "golden", "escape", "cash", "blip", "deny", "horn", "step_wood", "step_sand", "jump", "gull", "waves"]:
		if not Sfx.has_sound(s):
			print("[smoke] missing sound %s" % s)
			ok = false
	print("[smoke] built %d creature models x2 and checked sounds" % Data.CREATURES.size())

	# ferry is locked until you have species + money
	if Game.unlock_island(1):
		print("[smoke] coral unlocked too early")
		ok = false
	for id in ["puddle_minnow", "grumpy_perch", "stripe_bass", "mud_gulper", "pebblefin"]:
		Game.journal[id] = {"count": 1, "best": 1.0}
	Game.money = 10000
	ok = ok and Game.unlock_island(1)
	main.hud.travel_to(1)
	await get_tree().create_timer(2.5).timeout
	var d: float = main.player.global_position.distance_to(World.spawn_point(1))
	print("[smoke] travelled to %s, distance from spawn %.2f" % [Game.island()["name"], d])
	ok = ok and Game.current_island == 1 and d < 3.0

	# island exclusives show up in the catch table (Glowberry on Coral Key -> Candy Wrasse / Prism Squid)
	var rng := RandomNumberGenerator.new()
	rng.seed = 5
	var seen := {}
	for i in range(3000):
		seen[Data.roll_creature(2, 1, rng)] = true
	var brine_seen := {}
	for i in range(3000):
		brine_seen[Data.roll_creature(2, 0, rng)] = true
	print("[smoke] coral glowberry pool=%s" % [seen.keys()])
	ok = ok and seen.has("candy_wrasse") and seen.has("prism_squid") and not brine_seen.has("candy_wrasse")

	# store limits follow the island: Coral Key sells the Carbon Rod but not the Storm Rod
	Game.rod_level = 1
	ok = ok and Game.buy_rod() and Game.rod_level == 2 and not Game.buy_rod()

	# catch something on Coral Key to make sure fishing works off the starting island
	var r := "no_bite"
	for attempt in range(4):
		r = await _fish_once()
		if r == "caught":
			break
	print("[smoke] coral fishing result=%s cooler=%s" % [r, Game.cooler])
	ok = ok and r == "caught"
	return ok


func _net_host() -> bool:
	Net.my_name = "HostTester"
	var err := Net.host()
	if err != "":
		print("[smoke] host error: " + err)
		return false
	main.start_game()
	print("[smoke] hosting, waiting for client")
	var state_seen := [false]
	var catch_seen := [false]
	Net.state_received.connect(func(_id: int, s: Dictionary) -> void: state_seen[0] = s.has("p"))
	Net.event_received.connect(func(_id: int, kind: String, _d: Dictionary) -> void:
		if kind == "catch":
			catch_seen[0] = true)
	var t := 0.0
	var next_send := 0.0
	while not (state_seen[0] and catch_seen[0]) and t < 25.0:
		await get_tree().process_frame
		t += get_process_delta_time()
		if Net.players.size() > 0 and t >= next_send:
			next_send = t + 1.0
			Net.send_event("catch", {"id": "moon_carp", "size": 1.1, "golden": true, "value": 231})
	print("[smoke] host: players=%s state_seen=%s catch_seen=%s avatars=%d" % [Net.players, state_seen[0], catch_seen[0], main.avatars.size()])
	var ok: bool = state_seen[0] and catch_seen[0] and main.avatars.size() == 1
	await get_tree().create_timer(1.5).timeout  # let the client finish its checks
	return ok


func _net_join(address: String) -> bool:
	Net.my_name = "ClientTester"
	var err := Net.join(address)
	if err != "":
		print("[smoke] join error: " + err)
		return false
	var connected := [false]
	Net.connected.connect(func() -> void: connected[0] = true)
	var t := 0.0
	while not connected[0] and t < 10.0:
		await get_tree().process_frame
		t += get_process_delta_time()
	if not connected[0]:
		print("[smoke] client could not connect")
		return false
	main.start_game()
	var catch_seen := [false]
	Net.event_received.connect(func(_id: int, kind: String, d: Dictionary) -> void:
		if kind == "catch":
			catch_seen[0] = true
			print("[smoke] client saw catch event %s" % d))
	t = 0.0
	while not (catch_seen[0] and main.avatars.size() == 1) and t < 15.0:
		await get_tree().process_frame
		t += get_process_delta_time()
		Net.send_state(main.player.net_state())
	Net.send_event("catch", {"id": "glimmer_eel", "size": 0.9, "golden": false, "value": 50})
	await get_tree().create_timer(0.5).timeout
	var host_name := Net.player_name(1)
	var avatar_ok: bool = main.avatars.has(1) and main.avatars[1].visible
	print("[smoke] client: host_name=%s avatars=%d avatar_visible=%s catch_seen=%s" % [host_name, main.avatars.size(), avatar_ok, catch_seen[0]])
	return catch_seen[0] and host_name == "HostTester" and main.avatars.size() == 1


## Screenshots for eyeballing art: every creature model in a lineup, then each island from the sea.
func _gallery() -> bool:
	if _shots_dir == "":
		print("[smoke] --smoke-gallery needs --shots=DIR")
		return false
	var cam := Camera3D.new()
	main.add_child(cam)
	cam.current = true
	var stage := Node3D.new()
	stage.position = Vector3(0, 60, 0)  # in the sky, away from the island
	main.add_child(stage)
	var light := OmniLight3D.new()
	light.position = Vector3(0, 4, 6)
	light.omni_range = 30.0
	light.light_energy = 2.0
	stage.add_child(light)
	var ids: Array = Data.CREATURES.keys()
	var cols := 6
	for n in range(ids.size()):
		var id: String = ids[n]
		var model := FishModel.build(id, 1.0, n == ids.size() - 1)
		var length := float(Data.CREATURES[id]["length"])
		model.scale = Vector3.ONE * clampf(1.2 / length, 0.4, 4.0)
		model.position = Vector3((n % cols - (cols - 1) / 2.0) * 1.9, 2.2 - floor(n / cols) * 1.6, 0)
		model.rotation = Vector3(0.25, -0.5, 0)
		stage.add_child(model)
		var tag := Label3D.new()
		tag.text = Data.CREATURES[id]["name"]
		tag.font_size = 28
		tag.pixel_size = 0.006
		tag.position = model.position + Vector3(0, -0.6, 0.3)
		stage.add_child(tag)
	cam.global_position = stage.position + Vector3(0, 0.6, 9.5)
	cam.look_at(stage.position + Vector3(0, 0.6, 0))
	main.hud.visible = false
	await _frames(10)
	await _shot("gallery_fish")
	stage.queue_free()
	for i in range(Data.ISLANDS.size()):
		var o: Vector3 = Data.ISLANDS[i]["origin"]
		main.water.follow = cam
		cam.global_position = o + Vector3(38, 14, 52)
		cam.look_at(o + Vector3(0, 2, 0))
		await _frames(12)
		await _shot("island_%d_%s" % [i, Data.ISLANDS[i]["id"]])
	# a friend's avatar holding up a catch
	var a := Avatar.new()
	a.display_name = "Sam"
	a.body_color = Color(0.3, 0.6, 0.95)
	main.add_child(a)
	var spot := World.spawn_point(0)
	a.apply_state({"p": spot + Vector3(0, -0.2, -3), "y": 0.0, "x": 0.0, "b": spot + Vector3(0, -1.2, -14)})
	a.show_catch("moon_carp", 1.1, true)
	cam.global_position = spot + Vector3(2.5, 1.4, -8.0)
	cam.look_at(spot + Vector3(0, 1.4, -3))
	main.water.follow = cam
	await _frames(15)
	await _shot("avatar")
	return true
