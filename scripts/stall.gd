class_name Stall
extends StaticBody3D
## Interactable counter: "market" buys your whole cooler, "store" opens the shop, "ferry" opens travel.

var kind := "market"


func _ready() -> void:
	add_to_group("stalls")


func get_prompt() -> String:
	match kind:
		"market":
			if Game.cooler.is_empty():
				return "Fish Market (cooler empty)"
			var total := 0
			for c in Game.cooler:
				total += int(c["value"])
			return "Sell %d catch%s for $%d" % [Game.cooler.size(), "" if Game.cooler.size() == 1 else "es", total]
		"ferry":
			return "Take the ferry"
	return "Browse the General Store"


func interact(player: Player) -> void:
	match kind:
		"market":
			var count := Game.cooler.size()
			var total := Game.sell_all()
			if total == 0:
				Sfx.play("deny")
				Game.notify("\"Bring me something that swims, friend.\"", Color(0.9, 0.9, 0.8))
			else:
				Sfx.play("cash")
				Game.notify("Sold %d catch%s for $%d!" % [count, "" if count == 1 else "es", total], Color(0.5, 1.0, 0.5))
				Net.send_event("sold", {"total": total})
		"ferry":
			player.hud.open_travel()
		_:
			player.hud.open_shop()
