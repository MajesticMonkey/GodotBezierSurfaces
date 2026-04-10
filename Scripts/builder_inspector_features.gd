@tool
extends EditorInspectorPlugin

func _can_handle(object: Object) -> bool:
	return object is BezierSurfaceBuilder

func _parse_begin(object: Object) -> void:
	var progress_bar = ProgressBar.new()
	progress_bar.max_value = 1
	progress_bar.value = object.LoadPercent
	add_custom_control(progress_bar)
	
	var save_button = Button.new()
	save_button.text = "Save Surfaces"
	save_button.pressed.connect(object.SaveSurfaces)
	add_custom_control(save_button)

	var reload_button = Button.new()
	reload_button.text = "Reload Surfaces"
	reload_button.pressed.connect(object.UpdateSurfaces)
	add_custom_control(reload_button)

	var reload_all_button = Button.new()
	reload_all_button.text = "Reload All Surfaces"
	reload_all_button.pressed.connect(object.UpdateAllSurfaces)
	add_custom_control(reload_all_button)
