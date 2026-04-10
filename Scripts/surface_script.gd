@tool
extends MeshInstance3D


func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		var selection = EditorInterface.get_selection().get_selected_nodes()
		for i in selection.size():
			if selection[i] == self:
				EditorInterface.edit_node(get_parent())
