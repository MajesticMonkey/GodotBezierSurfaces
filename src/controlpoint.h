#ifndef GDCP_H
#define GDCP_H

#include <vector>

#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/core/math.hpp>

#include <godot_cpp/classes/collision_shape3d.hpp>
#include <godot_cpp/classes/material.hpp>
#include <godot_cpp/classes/mesh_instance3d.hpp>

#include <godot_cpp/variant/array.hpp>
#include <godot_cpp/variant/vector2i.hpp>



namespace godot {
    class ControlPoint : public MeshInstance3D {
        GDCLASS(ControlPoint, MeshInstance3D);

        private:
            float Weight = 1.0f;
            Vector2i Loc = Vector2i(-1, -1);

        protected:
            static void _bind_methods ( );

        public:
            void set_weight ( const float &W );
            float get_weight ( ) const;

            void set_loc ( const Vector2i &L );
            Vector2i get_loc ( ) const;
    };
}

#endif