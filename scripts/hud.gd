class_name Hud
extends CanvasLayer
## All 2D UI: money, bait/cooler status, toasts, cast/fight meters, co-op feed,
## and the Store / Journal / Ferry / Pause panels.

signal leave_requested

var player: Player

var _money: Label
var _status: Label
var _players: Label
var _feed: VBoxContainer
var _prompt: Label
var _message: Label
var _help: Label
var _charge_bar: ProgressBar
var _fight_box: VBoxContainer
var _tension_bar: ProgressBar
var _tension_fill: StyleBoxFlat
var _fight_label: Label
var _fade: ColorRect
var _panels := {}  # name -> {"panel": PanelContainer, "list": VBoxContainer, "build": Callable}
var _open_panel := ""
var _message_tween: Tween


func _ready() -> void:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	_money = _label(root, 34, Color(1, 0.92, 0.4))
	_place(_money, Vector4(0, 0, 0, 0), Rect2(24, 16, 400, 50))

	_players = _label(root, 17, Color(0.75, 0.95, 1.0))
	_place(_players, Vector4(0, 0, 0, 0), Rect2(26, 64, 600, 30))

	_feed = VBoxContainer.new()
	_feed.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_feed)
	_place(_feed, Vector4(0, 0, 0, 0), Rect2(26, 96, 700, 200))

	_status = _label(root, 20, Color(0.95, 0.95, 0.95))
	_status.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_place(_status, Vector4(1, 0, 1, 0), Rect2(-520, 16, 500, 130))

	var cross := _label(root, 22, Color(1, 1, 1, 0.8))
	cross.text = "+"
	cross.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	cross.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_place(cross, Vector4(0.5, 0.5, 0.5, 0.5), Rect2(-10, -18, 20, 36))

	_message = _label(root, 26, Color.WHITE)
	_message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_place(_message, Vector4(0.5, 0, 0.5, 0), Rect2(-600, 150, 1200, 80))

	_prompt = _label(root, 24, Color(1, 1, 1))
	_prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_place(_prompt, Vector4(0.5, 1, 0.5, 1), Rect2(-400, -150, 800, 40))

	_charge_bar = _bar(root, Color(0.4, 0.8, 1.0))
	_place(_charge_bar, Vector4(0.5, 1, 0.5, 1), Rect2(-160, -200, 320, 18))

	_fight_box = VBoxContainer.new()
	root.add_child(_fight_box)
	_place(_fight_box, Vector4(0.5, 1, 0.5, 1), Rect2(-220, -270, 440, 80))
	_fight_label = _label(_fight_box, 22, Color.WHITE)
	_fight_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_tension_bar = _bar(_fight_box, Color(0.4, 1.0, 0.4))
	_tension_bar.custom_minimum_size = Vector2(440, 26)
	_tension_fill = _tension_bar.get_theme_stylebox("fill") as StyleBoxFlat

	_help = _label(root, 16, Color(1, 1, 1, 0.75))
	_place(_help, Vector4(0, 1, 0, 1), Rect2(20, -110, 700, 90))
	_help.text = "WASD move  ·  Shift sprint  ·  Space jump\nHold LMB charge, release cast  ·  Hold LMB reel\nE interact  ·  1-4 bait  ·  Tab journal  ·  Esc menu"

	_add_panel(root, "shop", "General Store", _build_shop)
	_add_panel(root, "journal", "Field Journal", _build_journal)
	_add_panel(root, "travel", "Ferry", _build_travel)
	_add_panel(root, "pause", "Paused", _build_pause)

	_fade = ColorRect.new()
	_fade.color = Color(0, 0, 0, 0)
	_fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_fade)
	_place(_fade, Vector4(0, 0, 1, 1), Rect2(0, 0, 0, 0))

	Game.changed.connect(_refresh)
	Game.message.connect(show_message)
	_refresh()


## Anchor a control: anchors = (left, top, right, bottom); rect = offset from those anchors.
func _place(c: Control, anchors: Vector4, rect: Rect2) -> void:
	c.anchor_left = anchors.x
	c.anchor_top = anchors.y
	c.anchor_right = anchors.z
	c.anchor_bottom = anchors.w
	c.offset_left = rect.position.x
	c.offset_top = rect.position.y
	c.offset_right = rect.position.x + rect.size.x
	c.offset_bottom = rect.position.y + rect.size.y


func _label(parent: Node, font_size: int, color: Color) -> Label:
	var l := Label.new()
	l.add_theme_font_size_override("font_size", font_size)
	l.add_theme_color_override("font_color", color)
	l.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.85))
	l.add_theme_constant_override("outline_size", 6)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(l)
	return l


func _bar(parent: Node, color: Color) -> ProgressBar:
	var b := ProgressBar.new()
	b.min_value = 0.0
	b.max_value = 1.0
	b.show_percentage = false
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0, 0, 0, 0.55)
	bg.set_corner_radius_all(6)
	var fill := StyleBoxFlat.new()
	fill.bg_color = color
	fill.set_corner_radius_all(6)
	b.add_theme_stylebox_override("background", bg)
	b.add_theme_stylebox_override("fill", fill)
	b.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(b)
	return b


func _add_panel(root: Control, panel_name: String, title: String, build: Callable) -> void:
	var p := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.06, 0.1, 0.14, 0.94)
	style.set_corner_radius_all(12)
	style.set_content_margin_all(22)
	p.add_theme_stylebox_override("panel", style)
	p.visible = false
	root.add_child(p)
	_place(p, Vector4(0.5, 0.5, 0.5, 0.5), Rect2(-340, -330, 680, 660))
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 10)
	p.add_child(v)
	var t := _label(v, 30, Color(1, 0.92, 0.5))
	t.text = title
	var list := VBoxContainer.new()
	list.add_theme_constant_override("separation", 8)
	v.add_child(list)
	var close := Button.new()
	close.text = "Close  [Esc]"
	close.add_theme_font_size_override("font_size", 20)
	close.pressed.connect(close_panel)
	v.add_child(close)
	_panels[panel_name] = {"panel": p, "list": list, "build": build}


# ------------------------------------------------------------------ per-frame + input

func _process(_delta: float) -> void:
	if not player:
		return
	var f := player.fishing
	_charge_bar.visible = f.state == Fishing.State.CHARGING
	_charge_bar.value = f.charge
	_fight_box.visible = f.state == Fishing.State.FIGHT
	if _fight_box.visible:
		_tension_bar.value = minf(f.tension, 1.0)
		var danger := f.tension > Data.CLEAN_TENSION_LIMIT
		_tension_fill.bg_color = Color(1, 0.25, 0.2) if danger else Color(0.4, 1.0, 0.4).lerp(Color(1, 0.85, 0.2), f.tension / Data.CLEAN_TENSION_LIMIT)
		var left := maxf(f.distance - Fishing.LAND_DISTANCE, 0.0)
		var run := maxf(f.max_distance - f.distance, 0.0)
		_fight_label.text = "TENSION  ·  %.1f m to go  ·  escapes in %.1f m" % [left, run]


func _unhandled_input(event: InputEvent) -> void:
	var k := event as InputEventKey
	if not (k and k.pressed and not k.echo) or _open_panel == "" or player.automation:
		return
	var key: int = k.physical_keycode
	if key == KEY_ESCAPE or (key == KEY_E and _open_panel in ["shop", "travel"]) or (key == KEY_TAB and _open_panel == "journal"):
		close_panel()
		get_viewport().set_input_as_handled()


# ------------------------------------------------------------------ toasts + status

func set_prompt(text: String) -> void:
	_prompt.text = text


func show_message(text: String, color: Color = Color.WHITE) -> void:
	_message.text = text
	_message.add_theme_color_override("font_color", color)
	_message.modulate.a = 1.0
	if _message_tween:
		_message_tween.kill()
	_message_tween = create_tween()
	_message_tween.tween_interval(3.0)
	_message_tween.tween_property(_message, "modulate:a", 0.0, 1.0)


## A line in the co-op feed (top-left) that fades after a few seconds.
func feed(text: String, color: Color = Color(0.85, 0.95, 1.0)) -> void:
	var l := _label(_feed, 18, color)
	l.text = text
	if _feed.get_child_count() > 5:
		_feed.get_child(0).queue_free()
	var tw := create_tween()
	tw.tween_interval(6.0)
	tw.tween_property(l, "modulate:a", 0.0, 1.0)
	tw.tween_callback(l.queue_free)


func refresh_players() -> void:
	if not Net.is_online():
		_players.text = ""
		return
	var names := PackedStringArray(["You" + (" (host)" if Net.is_host() else "")])
	for id in Net.players.keys():
		names.append(Net.player_name(id) + (" (host)" if id == 1 else ""))
	_players.text = "Co-op: " + "  ·  ".join(names)


func _refresh() -> void:
	_money.text = "$%d" % Game.money
	var b: Dictionary = Game.bait()
	var count := "∞" if bool(b["infinite"]) else str(Game.bait_counts[Game.current_bait])
	_status.text = "%s\nBait: %s (%s)  [1-4]\n%s: %d / %d\n%s" % [
		Game.island()["name"], b["name"], count, Data.COOLERS[Game.cooler_level]["name"],
		Game.cooler.size(), Game.cooler_slots(), Game.rod()["name"]]
	if _open_panel != "":
		_rebuild(_open_panel)


# ------------------------------------------------------------------ panels

func is_panel_open() -> bool:
	return _open_panel != ""


func open_panel(panel_name: String) -> void:
	for n in _panels.keys():
		_panels[n]["panel"].visible = n == panel_name
	_open_panel = panel_name
	player.ui_open = true
	_message.modulate.a = 0.0  # don't let toasts bleed through the panel
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	_rebuild(panel_name)


func close_panel() -> void:
	if _open_panel == "":
		return
	_panels[_open_panel]["panel"].visible = false
	_open_panel = ""
	player.ui_open = false
	player.capture_mouse()


func open_shop() -> void:
	open_panel("shop")


func close_shop() -> void:
	close_panel()


func open_travel() -> void:
	open_panel("travel")


func open_pause() -> void:
	open_panel("pause")


func toggle_journal() -> void:
	if _open_panel == "journal":
		close_panel()
	elif _open_panel == "":
		open_panel("journal")


func _rebuild(panel_name: String) -> void:
	var list: VBoxContainer = _panels[panel_name]["list"]
	for c in list.get_children():
		c.queue_free()
	_panels[panel_name]["build"].call(list)


func _button(list: VBoxContainer, text: String, enabled: bool, action: Callable) -> void:
	var btn := Button.new()
	btn.text = text
	btn.disabled = not enabled
	btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
	btn.add_theme_font_size_override("font_size", 19)
	btn.pressed.connect(func() -> void:
		Sfx.play("blip")
		action.call())
	list.add_child(btn)


func _buy(ok: bool) -> void:
	if not ok:
		Sfx.play("deny")


func _build_shop(list: VBoxContainer) -> void:
	var isl := Game.island()
	var head := _label(list, 18, Color(0.8, 0.9, 1.0))
	head.text = "Wallet: $%d   ·   %s store" % [Game.money, isl["name"]]
	for i in isl["baits"]:
		var b: Dictionary = Data.BAITS[i]
		if bool(b["infinite"]):
			continue
		_button(list, "Bait: %d× %s  ($%d)  you have %d" % [b["pack"], b["name"], b["cost"], Game.bait_counts[i]],
			Game.money >= int(b["cost"]), func() -> void: _buy(Game.buy_bait(i)))
	var next_rod := Game.rod_level + 1
	if next_rod >= Data.RODS.size():
		_button(list, "Rod: %s (best rod)" % Game.rod()["name"], false, func() -> void: pass)
	elif next_rod > int(isl["max_rod"]):
		_button(list, "Rod: %s is sold on another island" % Data.RODS[next_rod]["name"], false, func() -> void: pass)
	else:
		var r: Dictionary = Data.RODS[next_rod]
		_button(list, "Rod: %s  ($%d)  faster reel, tougher line" % [r["name"], r["cost"]],
			Game.money >= int(r["cost"]), func() -> void: _buy(Game.buy_rod()))
	var next_cooler := Game.cooler_level + 1
	if next_cooler >= Data.COOLERS.size():
		_button(list, "Storage: %s (biggest)" % Data.COOLERS[Game.cooler_level]["name"], false, func() -> void: pass)
	elif next_cooler > int(isl["max_cooler"]):
		_button(list, "Storage: %s is sold on another island" % Data.COOLERS[next_cooler]["name"], false, func() -> void: pass)
	else:
		var c: Dictionary = Data.COOLERS[next_cooler]
		_button(list, "Storage: %s  ($%d)  holds %d" % [c["name"], c["cost"], c["slots"]],
			Game.money >= int(c["cost"]), func() -> void: _buy(Game.buy_cooler()))


func _build_journal(list: VBoxContainer) -> void:
	var text := RichTextLabel.new()
	text.bbcode_enabled = true
	text.fit_content = true
	text.add_theme_font_size_override("normal_font_size", 17)
	text.custom_minimum_size = Vector2(620, 480)
	list.add_child(text)
	var lines := PackedStringArray()
	for id in Data.CREATURES.keys():
		var c: Dictionary = Data.CREATURES[id]
		var rc: Color = Data.RARITY_COLORS[int(c["rarity"])]
		if Game.journal.has(id):
			var e: Dictionary = Game.journal[id]
			var best_cm := int(round(float(c["length"]) * float(e["best"]) * 100.0))
			lines.append("[color=#%s]%s[/color]  ×%d   best %d cm   base $%d" % [rc.to_html(false), c["name"], e["count"], best_cm, c["worth"]])
		else:
			lines.append("[color=#666666]???  (%s)[/color]" % Data.RARITY_NAMES[int(c["rarity"])])
	text.text = "Discovered %d / %d   ·   Lifetime earnings $%d\n\n%s" % [Game.species_count(), Data.CREATURES.size(), Game.lifetime_earnings, "\n".join(lines)]


func _build_travel(list: VBoxContainer) -> void:
	var head := _label(list, 18, Color(0.8, 0.9, 1.0))
	head.text = "Wallet: $%d   ·   Species discovered: %d" % [Game.money, Game.species_count()]
	for i in range(Data.ISLANDS.size()):
		var isl: Dictionary = Data.ISLANDS[i]
		if i == Game.current_island:
			_button(list, "%s  (you are here)" % isl["name"], false, func() -> void: pass)
		elif i in Game.unlocked_islands:
			_button(list, "Sail to %s" % isl["name"], true, func() -> void: travel_to(i))
		else:
			var reason := Game.unlock_block_reason(i)
			if reason == "":
				_button(list, "Buy ticket to %s  ($%d)" % [isl["name"], isl["cost"]], true, func() -> void:
					if Game.unlock_island(i):
						Sfx.play("cash")
						travel_to(i))
			else:
				_button(list, "%s  (locked: %s)" % [isl["name"], reason], false, func() -> void: pass)


func _build_pause(list: VBoxContainer) -> void:
	if Net.is_host():
		var info := _label(list, 18, Color(0.75, 0.95, 1.0))
		var addrs := Net.local_addresses()
		info.text = "Hosting on port %d.\nFriends on your network join with: %s\nOver the internet: forward UDP %d on your router and share your public IP." % [
			Net.DEFAULT_PORT, ", ".join(addrs) if addrs.size() > 0 else "your computer's IP", Net.DEFAULT_PORT]
		info.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		info.custom_minimum_size = Vector2(620, 0)
	_button(list, "Resume", true, close_panel)
	_button(list, "Leave to main menu" if Net.is_online() else "Main menu", true, func() -> void: leave_requested.emit())
	_button(list, "Quit game", true, func() -> void:
		Net.leave()
		Sfx.quit_cleanly())


# ------------------------------------------------------------------ travel

func travel_to(i: int) -> void:
	close_panel()
	player.fishing.reset()
	Sfx.play("horn")
	var tw := create_tween()
	tw.tween_property(_fade, "color:a", 1.0, 0.6)
	tw.tween_callback(func() -> void:
		Game.travel(i)
		player.respawn()
		Net.send_event("travel", {"island": i}))
	tw.tween_interval(0.4)
	tw.tween_property(_fade, "color:a", 0.0, 0.8)
	tw.tween_callback(func() -> void:
		show_message("Welcome to %s!" % Data.ISLANDS[i]["name"], Color(0.8, 0.95, 1.0)))
