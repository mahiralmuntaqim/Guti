extends Node
## Co-op networking over ENet (Godot high-level multiplayer).
## Model: every player owns their own money/gear/catches (saved locally). The network shares
## presence (where everyone is, their rod/bobber state) and events (catches, travel, chat-style toasts).
## The host relays traffic between clients (SceneMultiplayer server relay), so clients only need the host's address.

signal player_joined(id: int)
signal player_left(id: int)
signal state_received(id: int, state: Dictionary)
signal event_received(id: int, kind: String, data: Dictionary)
signal connected
signal connection_failed(reason: String)
signal disconnected

const DEFAULT_PORT := 24680
const MAX_PLAYERS := 8

var my_name := "Angler"
var my_color := Color(0.9, 0.4, 0.3)
var players := {}  # peer id -> {"name": String, "color": Color}


func _ready() -> void:
	multiplayer.peer_connected.connect(_on_peer_connected)
	multiplayer.peer_disconnected.connect(_on_peer_disconnected)
	multiplayer.connected_to_server.connect(func() -> void: connected.emit())
	multiplayer.connection_failed.connect(func() -> void:
		_reset_peer()
		connection_failed.emit("Couldn't reach the host."))
	multiplayer.server_disconnected.connect(func() -> void:
		_reset_peer()
		disconnected.emit())


func is_online() -> bool:
	return multiplayer.multiplayer_peer != null and not (multiplayer.multiplayer_peer is OfflineMultiplayerPeer)


func is_host() -> bool:
	return is_online() and multiplayer.is_server()


func my_id() -> int:
	return multiplayer.get_unique_id() if is_online() else 1


func host(port := DEFAULT_PORT) -> String:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_server(port, MAX_PLAYERS)
	if err != OK:
		return "Couldn't host on port %d (%s). Is another copy already hosting?" % [port, error_string(err)]
	multiplayer.multiplayer_peer = peer
	return ""


func join(address: String, port := DEFAULT_PORT) -> String:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_client(address, port)
	if err != OK:
		return "Couldn't start connecting (%s)." % error_string(err)
	multiplayer.multiplayer_peer = peer
	return ""


func leave() -> void:
	if is_online():
		multiplayer.multiplayer_peer.close()
	_reset_peer()


func _reset_peer() -> void:
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	for id in players.keys():
		player_left.emit(id)
	players.clear()


func local_addresses() -> PackedStringArray:
	var out := PackedStringArray()
	for a in IP.get_local_addresses():
		if a.count(".") == 3 and not a.begins_with("127.") and not a.begins_with("169.254."):
			out.append(a)
	return out


func player_name(id: int) -> String:
	return String(players.get(id, {}).get("name", "Player %d" % id))


func send_state(state: Dictionary) -> void:
	if is_online() and not players.is_empty():
		_state.rpc(state)


func send_event(kind: String, data: Dictionary = {}) -> void:
	if is_online() and not players.is_empty():
		_event.rpc(kind, data)


# ------------------------------------------------------------------ handshake + RPCs

func _on_peer_connected(id: int) -> void:
	# introduce ourselves to every peer we can see (host and, via relay, other clients)
	_register.rpc_id(id, my_name, my_color)


func _on_peer_disconnected(id: int) -> void:
	if players.has(id):
		players.erase(id)
		player_left.emit(id)


@rpc("any_peer", "reliable")
func _register(peer_name: String, color: Color) -> void:
	var id := multiplayer.get_remote_sender_id()
	var clean := peer_name.strip_edges().substr(0, 16)
	players[id] = {"name": clean if clean != "" else "Player %d" % id, "color": color}
	player_joined.emit(id)


@rpc("any_peer", "unreliable_ordered")
func _state(state: Dictionary) -> void:
	var id := multiplayer.get_remote_sender_id()
	if players.has(id):
		state_received.emit(id, state)


@rpc("any_peer", "reliable")
func _event(kind: String, data: Dictionary) -> void:
	var id := multiplayer.get_remote_sender_id()
	if players.has(id):
		event_received.emit(id, kind, data)
