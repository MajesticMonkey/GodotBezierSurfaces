@tool
extends MeshInstance3D

var generated_mesh: Mesh

func _notification(what):
	if Engine.is_editor_hint():
		match what:
			NOTIFICATION_EDITOR_PRE_SAVE:
				generated_mesh = mesh
				mesh = null
			NOTIFICATION_EDITOR_POST_SAVE:
				mesh = generated_mesh

func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		var selection = EditorInterface.get_selection().get_selected_nodes()
		for i in selection.size():
			if selection[i] == self:
				EditorInterface.edit_node(get_parent())
