#ifndef GDNEP_H
#define GDNEP_H

#include <vector>

#include <godot_cpp/core/math.hpp>

#include <godot_cpp/classes/editor_plugin.hpp>
#include <godot_cpp/classes/mesh_instance3d.hpp>

#include <godot_cpp/variant/vector3.hpp>

namespace godot {
    class NURBsEditorPlugin : public EditorPlugin {
        GDCLASS(NURBsEditorPlugin, EditorPlugin);

        protected:
            static void _bind_methods();
    };
}

#endif

// Use this file to add drag and drop NURB binding eventually.