using Godot;
using System;
using static BezierSurfaceBuilder.BezierSurfaceBuilder;

namespace BezierSurfaces
{
	[GlobalClass, Tool]
	public partial class ControlPoint : MeshInstance3D
	{
		public Vector2 Loc = new Vector2(0, 0);

		private bool HasSurface = false;


		public void CreateSurface()
		{
			if (!HasSurface)
			{
				HasSurface = true;
				NotifyPropertyListChanged();
				var forklift = GetParent();
				if (forklift is BezierSurfaceBuilder.BezierSurfaceBuilder builder)
				{
					builder.CreateSurfaceExternally(Loc);
				}
			}
			else
			{
				NotifyPropertyListChanged();
			}
		}

		public void RemoveSurface()
		{
			if (HasSurface)
			{
				HasSurface = false;
				NotifyPropertyListChanged();
				var forklift = GetParent();
				if (forklift is BezierSurfaceBuilder.BezierSurfaceBuilder builder)
				{
					builder.RemoveSurfaceExternally(Loc);
				}
			}
			else
			{
				NotifyPropertyListChanged();
			}
		}
	}
}
