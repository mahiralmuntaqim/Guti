class_name MainMenu
extends CanvasLayer
## Title screen: choose a name, then play solo, host a co-op session, or join a friend by IP.

signal solo_pressed
signal host_pressed
signal join_pressed(address: String)

const SETTINGS_PATH := "user://settings.cfg"

var name_edit: LineEdit
var ip_edit: LineEdit
var _status: Label
var _buttons: Array[Button] = []


func _ready() -> void:
	var bg := ColorRect.new()
	bg.color = Color(0.02, 0.08, 0.14, 0.55)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)

	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.06, 0.1, 0.14, 0.95)
	style.set_corner_radius_all(16)
	style.set_content_margin_all(32)
	panel.add_theme_stylebox_override("panel", style)
	panel.anchor_left = 0.5
	panel.anchor_right = 0.5
	panel.anchor_top = 0.5
	panel.anchor_bottom = 0.5
	panel.offset_left = -300
	panel.offset_right = 300
	panel.offset_top = -300
	panel.offset_bottom = 300
	add_child(panel)

	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 12)
	panel.add_child(v)

	var title := Label.new()
	title.text = "BRINE TIME"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 56)
	title.add_theme_color_override("font_color", Color(1, 0.92, 0.5))
	v.add_child(title)
	var sub := Label.new()
	sub.text = "cast  ·  fight  ·  sell  ·  sail"
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	sub.add_theme_color_override("font_color", Color(0.7, 0.85, 0.95))
	v.add_child(sub)

	v.add_child(_caption("Your name"))
	name_edit = LineEdit.new()
	name_edit.max_length = 16
	name_edit.placeholder_text = "Angler"
	v.add_child(name_edit)

	_buttons.append(_big_button(v, "Play Solo", func() -> void: solo_pressed.emit()))
	_buttons.append(_big_button(v, "Host Co-op  (port %d)" % Net.DEFAULT_PORT, func() -> void: host_pressed.emit()))

	v.add_child(_caption("Friend's IP address"))
	var row := HBoxContainer.new()
	v.add_child(row)
	ip_edit = LineEdit.new()
	ip_edit.placeholder_text = "e.g. 192.168.1.20"
	ip_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	ip_edit.text_submitted.connect(func(_t: String) -> void: join_pressed.emit(ip_edit.text.strip_edges()))
	row.add_child(ip_edit)
	var join := Button.new()
	join.text = "  Join  "
	join.add_theme_font_size_override("font_size", 20)
	join.pressed.connect(func() -> void: join_pressed.emit(ip_edit.text.strip_edges()))
	row.add_child(join)
	_buttons.append(join)

	_status = Label.new()
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_status.add_theme_color_override("font_color", Color(1, 0.8, 0.5))
	_status.custom_minimum_size = Vector2(530, 50)
	v.add_child(_status)

	_big_button(v, "Quit", func() -> void: Sfx.quit_cleanly())

	var cfg := ConfigFile.new()
	if cfg.load(SETTINGS_PATH) == OK:
		name_edit.text = String(cfg.get_value("player", "name", ""))
		ip_edit.text = String(cfg.get_value("player", "last_ip", ""))
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _caption(text: String) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_color_override("font_color", Color(0.8, 0.85, 0.9))
	return l


func _big_button(parent: Node, text: String, action: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(0, 48)
	b.add_theme_font_size_override("font_size", 22)
	b.pressed.connect(action)
	parent.add_child(b)
	return b


func player_name() -> String:
	var n := name_edit.text.strip_edges()
	return n if n != "" else "Angler"


func remember() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("player", "name", player_name())
	cfg.set_value("player", "last_ip", ip_edit.text.strip_edges())
	cfg.save(SETTINGS_PATH)


func set_status(text: String) -> void:
	_status.text = text


func set_busy(busy: bool) -> void:
	for b in _buttons:
		b.disabled = busy
