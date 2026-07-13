#include "controlpoint.h"

#include <godot_cpp/core/class_db.hpp>

using namespace godot;

void ControlPoint::_bind_methods (

)
{
    ClassDB::bind_method(D_METHOD("SetWeight", "W"), &ControlPoint::SetWeight);
    ClassDB::bind_method(D_METHOD("GetWeight"), &ControlPoint::GetWeight);

    ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "Weight"), "SetWeight", "GetWeight");
}

void ControlPoint::SetWeight ( const float &W ) { Weight = W; }

float ControlPoint::GetWeight ( ) const { return Weight; }