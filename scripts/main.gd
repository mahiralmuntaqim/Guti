extends Node3D
## Entry point: builds the world, player, HUD and title menu, and wires up co-op networking.
## Automated tests live in tests.gd (run with `-- --smoke`, `-- --smoke-islands`,
## `-- --smoke-host`, `-- --smoke-join=127.0.0.1`).

const STATE_SEND_INTERVAL := 0.05  # 20 Hz presence updates

var world: World
var player: Player
var hud: Hud
var water: Water
var menu: MainMenu
var avatars := {}  # peer id -> Avatar

var _state_timer := 0.0
var _in_game := false


func _ready() -> void:
	world = World.new()
	add_child(world)

	water = Water.new()
	add_child(water)

	player = Player.new()
	add_child(player)
	player.spawn_yaw = PI  # face +Z, out to sea
	player.respawn()

	hud = Hud.new()
	hud.player = player
	player.hud = hud
	add_child(hud)
	hud.leave_requested.connect(_leave_to_menu)
	water.follow = player

	Net.player_joined.connect(_on_player_joined)
	Net.player_left.connect(_on_player_left)
	Net.state_received.connect(_on_state)
	Net.event_received.connect(_on_event)
	Net.disconnected.connect(_on_disconnected)

	var args := OS.get_cmdline_user_args()
	if "--look-back" in args:  # screenshot helper: face the island instead of the sea
		player.rotation.y = 0.0
		player.camera.rotation.x = deg_to_rad(-8)
	for a in args:
		if a.begins_with("--smoke"):
			var tests: Node = preload("res://scripts/tests.gd").new()
			tests.main = self
			add_child(tests)
			return
	if "--no-menu" in args:
		start_game()
		return
	_show_menu()


# ------------------------------------------------------------------ menu / session flow

func _show_menu() -> void:
	player.ui_open = true
	menu = MainMenu.new()
	add_child(menu)
	menu.solo_pressed.connect(func() -> void:
		menu.remember()
		start_game())
	menu.host_pressed.connect(_menu_host)
	menu.join_pressed.connect(_menu_join)


func _menu_host() -> void:
	menu.remember()
	Net.my_name = menu.player_name()
	Net.my_color = _name_color(Net.my_name)
	var err := Net.host()
	if err != "":
		menu.set_status(err)
		return
	start_game()
	var addrs := Net.local_addresses()
	hud.feed("Hosting! Friends join with: %s  (port %d)" % [", ".join(addrs) if addrs.size() > 0 else "your IP", Net.DEFAULT_PORT], Color(0.6, 1.0, 0.7))
	hud.feed("Press Esc any time to see the join address again.", Color(0.75, 0.9, 1.0))


func _menu_join(address: String) -> void:
	if address == "":
		menu.set_status("Type your friend's IP address first.")
		return
	menu.remember()
	Net.my_name = menu.player_name()
	Net.my_color = _name_color(Net.my_name)
	var err := Net.join(address)
	if err != "":
		menu.set_status(err)
		return
	menu.set_status("Connecting to %s:%d ..." % [address, Net.DEFAULT_PORT])
	menu.set_busy(true)
	var result: Array = await _await_connection()
	if not is_instance_valid(menu):
		return
	if result[0]:
		start_game()
	else:
		menu.set_busy(false)
		menu.set_status("Couldn't connect: %s  Check the IP, that the host pressed \"Host Co-op\", and that their firewall allows the game." % result[1])


func _await_connection() -> Array:
	var outcome := []
	var on_ok := func() -> void: outcome.append_array([true, ""])
	var on_fail := func(reason: String) -> void: outcome.append_array([false, reason])
	Net.connected.connect(on_ok, CONNECT_ONE_SHOT)
	Net.connection_failed.connect(on_fail, CONNECT_ONE_SHOT)
	var waited := 0.0
	while outcome.is_empty() and waited < 10.0:
		await get_tree().process_frame
		waited += get_process_delta_time()
	if Net.connected.is_connected(on_ok):
		Net.connected.disconnect(on_ok)
	if Net.connection_failed.is_connected(on_fail):
		Net.connection_failed.disconnect(on_fail)
	if outcome.is_empty():
		Net.leave()
		return [false, "timed out."]
	return outcome


func start_game() -> void:
	if is_instance_valid(menu):
		menu.queue_free()
	_in_game = true
	player.ui_open = false
	player.respawn()
	player.capture_mouse()
	hud.refresh_players()
	hud.show_message("Welcome to %s. Fish off the dock, sell at the Market, gear up at the Store, sail at the Ferry." % Game.island()["name"], Color(0.8, 0.95, 1.0))


func _leave_to_menu() -> void:
	Net.leave()
	get_tree().reload_current_scene()


func _on_disconnected() -> void:
	for id in avatars.keys():
		_on_player_left(id)
	if _in_game:
		hud.feed("Lost connection to the host. You're playing solo now.", Color(1, 0.6, 0.4))
		hud.refresh_players()


static func _name_color(n: String) -> Color:
	return Color.from_hsv(fmod(float(hash(n) % 1000) / 1000.0 + 1.0, 1.0), 0.6, 0.9)


# ------------------------------------------------------------------ co-op presence

func _physics_process(delta: float) -> void:
	if not _in_game or not Net.is_online():
		return
	_state_timer -= delta
	if _state_timer <= 0.0:
		_state_timer = STATE_SEND_INTERVAL
		Net.send_state(player.net_state())


func _on_player_joined(id: int) -> void:
	if avatars.has(id):
		return
	var info: Dictionary = Net.players[id]
	var a := Avatar.new()
	a.peer_id = id
	a.display_name = info["name"]
	a.body_color = info["color"]
	add_child(a)
	avatars[id] = a
	if _in_game:
		hud.feed("%s joined the island." % info["name"], Color(0.6, 1.0, 0.7))
		Sfx.play("blip", 1.3)
	hud.refresh_players()


func _on_player_left(id: int) -> void:
	if avatars.has(id):
		hud.feed("%s left." % avatars[id].display_name, Color(1.0, 0.8, 0.6))
		avatars[id].queue_free()
		avatars.erase(id)
	hud.refresh_players()


func _on_state(id: int, state: Dictionary) -> void:
	if avatars.has(id):
		avatars[id].apply_state(state)


func _on_event(id: int, kind: String, data: Dictionary) -> void:
	var who := Net.player_name(id)
	match kind:
		"catch":
			var cid := String(data.get("id", ""))
			if not Data.CREATURES.has(cid):
				return
			var golden := bool(data.get("golden", false))
			var size := float(data.get("size", 1.0))
			var c: Dictionary = Data.CREATURES[cid]
			var cm := int(round(float(c["length"]) * size * 100.0))
			var col: Color = Color(1, 0.85, 0.2) if golden else Data.RARITY_COLORS[int(c["rarity"])]
			hud.feed("%s caught a %s (%d cm, $%d)" % [who, Data.catch_title(cid, golden), cm, int(data.get("value", 0))], col)
			if avatars.has(id):
				avatars[id].show_catch(cid, size, golden)
				Sfx.play_at("splash_big", avatars[id].global_position, 1.0, -4.0)
			if golden or int(c["rarity"]) >= 2:
				Sfx.play("golden", 1.0, -8.0)
		"sold":
			hud.feed("%s sold their catch for $%d" % [who, int(data.get("total", 0))])
			if avatars.has(id):
				Sfx.play_at("cash", avatars[id].global_position, 1.0, -6.0)
		"travel":
			var i := int(data.get("island", 0))
			if i >= 0 and i < Data.ISLANDS.size():
				hud.feed("%s sailed to %s." % [who, Data.ISLANDS[i]["name"]])
		"splash":
			if avatars.has(id):
				Sfx.play_at("splash_big", avatars[id].global_position)
				hud.feed("%s fell in the water!" % who, Color(0.6, 0.85, 1.0))
