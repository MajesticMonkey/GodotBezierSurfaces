using Godot;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BezierSurfaces.Types.Matrix;
using BezierSurfaces.Types.VectorVariants.HalfVector;
using BezierSurfaces.Types.VectorVariants.ByteVector;
using BezierSurfaces.Types.VectorVariants.BitVector;
using BezierSurfaces;

using static System.Math;





namespace BezierSurfaceBuilder
{
	[GlobalClass, Tool]
	public partial class BezierSurfaceBuilder : Node3D
	{
		private List<BezierSurface> SurfaceNetwork = new List<BezierSurface>();
		public List<List<ControlPoint>> ControlNetwork = new List<List<ControlPoint>>();
		public List<List<Vector3>> ControlNetworkPositions = new List<List<Vector3>>();
		
		[ExportGroup("Do")]
		
		private BitVector3 Do = new BitVector3(true, true, true); // Determines whether or not we do the full Bezier transform calculation for a given direction, as it saves computing power and is equally accurate when the contol points are evenly spaced.

		[Export]
		public bool DoX = true;
		[Export]
		public bool DoY = true;
		[Export]
		public bool DoZ = true;

		[ExportGroup("Control Nodes")]
		
		[Export]
		public Mesh CNShape = new SphereMesh();
		
		[ExportSubgroup("Network Size")]
		
		[Export]
		public byte CNXSize = 4;
		[Export]
		public byte CNYSize = 4;
		
		private ByteVector2 CNSize = new ByteVector2(0, 0); // Control Net Size
		
		[ExportGroup("Vertex Map Size")]
		[Export]
		public byte SVMXSize = 32; // Surface Vertex Map X Size
		[Export]
		public byte SVMYSize = 32; // Surface Vertex Map Y Size
		
		private ByteVector2 SVMSize = new ByteVector2(0, 0);
		
		[ExportGroup("Surface Size")]
		[Export]
		public byte SXSize = 32; // Surface X Size
		[Export]
		public byte SYSize = 32; // Surface X Size

		private ByteVector2 SSize = new ByteVector2(0, 0);
		
		//[ExportGroup("Material")]
		//[Export]
		//public ShaderMaterial NormalShower = GD.Load<ShaderMaterial>("res://addons/beziersurfaces/Textures/NormalShower.tres");
		// Currently Defunct variable for applying a material or shader to the surface.

		private Matrix NB;
		private Matrix MB;
		private Matrix NBD;
		private Matrix MBD;

		private Vector2 ControlPointSpacing;

		readonly String BezierPrefix = "BezierSurface_";
		readonly String NodePrefix = "ControlPoint_";

		private float LoadPercent = 0;

		private Godot.ProgressBar ProgressBar = new Godot.ProgressBar();

		private Resource SurfaceScript = GD.Load("res://addons/beziersurfaces/Scripts/surface_script.gd");

		public BezierSurfaceBuilder()
		{
			UpdateMaintenance();
		}
		
		public override void _EnterTree()
		{
			Godot.Collections.Array<Godot.Node> children = GetChildren();
			for (int i = 0; i < children.Count; i++)
			{
				RemoveChild(children[i]);
				children[i].QueueFree();
			}
			
			CreateSurface(new Vector2(0, 0));
		}

		public override void _Process(double delta)
		{
			if (Engine.IsEditorHint())
			{
				SetDisplayFolded(true);
				EmitSignal("script_changed");
			}
		}

		public override void _ExitTree()
		{
			//pass;
		}

		public bool SaveSurfaces()
		{
			return false;
		}

		public void UpdateMaintenance()
		{
			Do = new BitVector3(DoX, DoY, DoZ);

			bool RecalcNB = !(CNSize.x == CNXSize);
			bool RecalcMB = !(CNSize.y == CNYSize);

			CNSize = new ByteVector2(CNXSize, CNYSize);
			SVMSize = new ByteVector2(SVMXSize, SVMYSize);
			SSize = new ByteVector2(SXSize, SYSize);

			if (RecalcNB)
			{
				NB = BernsteinPolynomial(CNSize.x);
				NBD = DifferentiateBernstein(CNSize.x);
			}
			if (RecalcMB)
			{
				MB = BernsteinPolynomial(CNSize.y).Transpose();
				MBD = DifferentiateBernstein(CNSize.y).Transpose();
			}

			ControlPointSpacing = new Vector2((float)SSize.x/((float)CNSize.x - (float)1), (float)SSize.y/((float)CNSize.y - (float)1));
		}

		public async void UpdateAllSurfaces()
		{
			UpdateMaintenance();
			LoadPercent = 0;
			Do = new BitVector3(DoX, DoY, DoZ);
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				SurfaceNetwork[i].ReloadSurface(SurfaceNetwork.Count, i);
			}
		}

		public async void UpdateSurfaces()
		{
			LoadPercent = 0;
			Do = new BitVector3(DoX, DoY, DoZ);
			List<ControlPoint> outdatedControlNodes = new List<ControlPoint>();
			for (int i = 0; i < ControlNetwork.Count; i++)
			{
				for (int j = 0; j < ControlNetwork[i].Count; j++)
				{
					if (ControlNetwork[i][j].Position != ControlNetworkPositions[i][j])
					{
						outdatedControlNodes.Add(ControlNetwork[i][j]);
					}
				}
			}

			List<BezierSurface> outdatedSurfaces = new List<BezierSurface>();
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				for (int j = 0; j < outdatedControlNodes.Count; j++)
				{
					if (SurfaceNetwork[i].IsMyControlNode(outdatedControlNodes[j].Loc))
					{
						outdatedSurfaces.Add(SurfaceNetwork[i]);
					}
				}
			}

			for (int i = 0; i < outdatedSurfaces.Count; i++)
			{
				outdatedSurfaces[i].ReloadSurface(outdatedSurfaces.Count, i);
			}
		}
		
		public void CreateSurfaceExternally(Vector2 Loc)
		{
			CreateSurface(Loc);
		}

		private BezierSurface CreateSurface(Vector2 Loc)
		{
			for (int i = 0; i < Loc.X + CNSize.x; i++)
			{
				if (i == ControlNetwork.Count && i != Loc.X + CNSize.x)
				{
					ControlNetwork.Add(new List<ControlPoint>());
					ControlNetworkPositions.Add(new List<Vector3>());
				}
				for (int j = 0; j < Loc.Y + CNSize.y; j++)
				{
					if (j == ControlNetwork[i].Count && j != Loc.Y + CNSize.y)
					{
						ControlNetwork[i].Add(ConstControlPoint(new Vector3(i, 0, j)));
						ControlNetwork[i][j].Position = new Vector3((float)i*ControlPointSpacing.X, 0, (float)j*ControlPointSpacing.Y);
						ControlNetworkPositions[i].Add(ControlNetwork[i][j].Position);
					}
				}
			}

			BezierSurface surface = new BezierSurface(this, Loc);

			SurfaceNetwork.Add(surface);
			
			return surface;
		}

		public void RemoveSurfaceExternally(Vector2 Loc)
		{
			RemoveSurface(Loc);
		}

		private void RemoveSurface(Vector2 Loc)
		{
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				if (SurfaceNetwork[i].CNLoc == Loc)
				{
					SurfaceNetwork[i].Patch.QueueFree();
					SurfaceNetwork.RemoveAt(i);
				}
			}
		}
		
		public ControlPoint ConstControlPoint(Vector3 Loc)
		{
			ControlPoint meshInstance = new ControlPoint();
			meshInstance.Mesh = CNShape;
			AddChild(meshInstance);
			var theTree = GetTree().GetEditedSceneRoot();
			meshInstance.SetOwner(theTree);
			meshInstance.Position = Loc;
			meshInstance.Name = NodePrefix + Loc.X.ToString() + "_" + Loc.Z.ToString();
			meshInstance.Loc = new Vector2(Loc.X, Loc.Z);
			return meshInstance;
		}
		
		private struct BezierSurface
		{
			public Vector2 CNLoc = new Vector2(0, 0);
			
			public Byte LOD = 1;
			
			public Vector3[,] CN;

			public MeshInstance3D Patch;

			readonly BezierSurfaceBuilder parent;

			#region Lambda Expressions
				List<List<ControlPoint>> ControlNetwork => parent.ControlNetwork;
				//ShaderMaterial NormalShower => parent.NormalShower;
				ByteVector2 CNSize => parent.CNSize;
				ByteVector2 SVMSize => parent.SVMSize;
				ByteVector2 SSize => parent.SSize;
				BitVector3 Do => parent.Do;
				Matrix NB => parent.NB;
				Matrix MB => parent.MB;
				Matrix NBD => parent.NBD;
				Matrix MBD => parent.MBD;

				Vector2 ControlPointSpacing => parent.ControlPointSpacing;

				float LoadPercent => parent.LoadPercent;

				String BezierPrefix => parent.BezierPrefix;
			#endregion
			
			
			public BezierSurface(BezierSurfaceBuilder Parent, Vector2 CNLocation)
			{
				parent = Parent; // this MUST be initalized first. Other values use lambda expressions to hide the use of the parent pointer
				
				CNLoc = CNLocation;
				
				Patch = CreatePatchMeshInstance();

				Patch.SetScript(parent.SurfaceScript);

				parent.AddChild(Patch); // Might cause issues that it's in this order.

				var theTree = parent.GetTree().GetEditedSceneRoot();
				Patch.SetOwner(theTree);
				
				ReloadSurface();
			}
			
			public void ReloadSurface(int SurfaceCount = 1, int SurfaceIndex = 0)
			{
				Godot.Collections.Array<Node> children = Patch.GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					children[i].QueueFree();
				}


				ArrayMesh arrMesh = CreateArrayMesh(SurfaceCount, SurfaceIndex);

				Patch.SetMesh(arrMesh);

				Patch.CreateTrimeshCollision();

				children = Patch.GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					if (children[i] is Node3D node3DChild)
					{
						node3DChild.SetOwner(null);
						node3DChild.Hide();
					}
				}
			}

			private Vector3[,] GetControlNodes()
			{
				Vector3[,] CN = new Vector3[CNSize.x, CNSize.y];
				for (int i = 0; i + CNLoc.X < ControlNetwork.Count && i < CNSize.x; i++)
				{
					for (int j = 0; j + CNLoc.Y < ControlNetwork[i + (int)CNLoc.X].Count && j < CNSize.y; j++)
					{
						CN[i, j] = ControlNetwork[i + (int)CNLoc.X][j + (int)CNLoc.Y].Position;
						if (!Do.x) { CN[i, j].X = ((CNLoc.X + i) * ControlPointSpacing.X); }
						if (!Do.y) { CN[i, j].Y = 0; }
						if (!Do.z) { CN[i, j].Z = ((CNLoc.Y + j) * ControlPointSpacing.Y); }
					}
				}
				return CN;
			}
			
			public bool IsMyControlNode(Vector2 CPLoc)
			{
				float dx = CPLoc.X - CNLoc.X;
				float dy = CPLoc.Y - CNLoc.Y;
				return ((dx >= 0) && (dy >= 0) && (dx < CNSize.x) && (dy < CNSize.y));
			}

			private MeshInstance3D CreatePatchMeshInstance()
			{
				MeshInstance3D Patch = new MeshInstance3D();

				Patch.Name = BezierPrefix + CNLoc.X.ToString() + "_" + CNLoc.Y.ToString();

				Patch.TopLevel = true;

				return Patch;
			}

			private ArrayMesh CreateArrayMesh(int SurfaceCount = 1, int SurfaceIndex = 0)
			{ 
				ByteVector2 LODedSVMSize = GetLODedSVMSize();


				CN = GetControlNodes();

				
				ArrayMesh ArrMesh = new ArrayMesh();


				Vector3[,] STM = GetSurfaceTransforms(SurfaceCount, SurfaceIndex);
				Vector3[,] SNM = GetSurfaceNormals(SurfaceCount, SurfaceIndex);


				var SurfaceArray = new Godot.Collections.Array();

				SurfaceArray.Resize((int)Mesh.ArrayType.Max);

				SurfaceArray[(int)Mesh.ArrayType.Vertex] = WindTriangles(STM);
				SurfaceArray[(int)Mesh.ArrayType.Normal] = WindTriangles(SNM);

				
				ArrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, SurfaceArray);

				//ArrMesh.SurfaceSetMaterial(0, NormalShower);


				return ArrMesh;
			}

			#region Create Array Mesh
			private Vector3[,] GetSurfaceTransforms(int SurfaceCount = 1, int SurfaceIndex = 0)
			{
				ByteVector2 LODedSVMSize = GetLODedSVMSize();

				Vector3[,] STMForklift = new Vector3[LODedSVMSize.x, LODedSVMSize.y];
				for (byte u = 0; u < LODedSVMSize.x; u++)
				{
					for (byte v = 0; v < LODedSVMSize.y; v++)
					{
						STMForklift[u, v] = ComputeVertexVector(u, v, 0);

						int count = u * LODedSVMSize.y + v;

						parent.LoadPercent =
						((float)SurfaceIndex/(float)SurfaceCount) +
						((float)count/((float)LODedSVMSize.x * (float)LODedSVMSize.y * (float)SurfaceCount * (float)2));

						// The math for the progress bar isn't that hard to understand, just read it a couple times.
					}
				}
				return STMForklift;
			}
			
			private Vector3[,] GetSurfaceNormals(int SurfaceCount = 1, int SurfaceIndex = 0)
			{
				ByteVector2 LODedSVMSize = GetLODedSVMSize();

				Vector3[,] SNMForklift = new Vector3[SVMSize.x, SVMSize.y];				
				
				float firsthalf = (float)1 / ((float)SurfaceCount * (float)2);
				
				for (byte u = 0; u < LODedSVMSize.x; u++)
				{
					for (byte v = 0; v < LODedSVMSize.y; v++)
					{
						SNMForklift[u, v] = ComputeVertexNormal(u, v);

						int count = u * LODedSVMSize.y + v;

						parent.LoadPercent =
						((float)SurfaceIndex/(float)SurfaceCount) + firsthalf +
						((float)count/((float)LODedSVMSize.x * (float)LODedSVMSize.y * (float)SurfaceCount * (float)2));
					}
				}


				return SNMForklift;
			}

			private Vector3 ComputeVertexNormal(byte u, byte v)
			{
				Vector3 TangentA = ComputeVertexVector(u, v, 1);
				Vector3 TangentB = ComputeVertexVector(u, v, 2);

				if (CN[0, 0].X < 0)
				{
					TangentA.X = Abs(TangentA.X);
					TangentA.Y = Abs(TangentA.Y);
					TangentA.Z = Abs(TangentA.Z);
				} else if (CN[0, 0].Z < 0)
				{
					TangentB.X = Abs(TangentB.X);
					TangentB.Y = Abs(TangentB.Y);
					TangentB.Z = Abs(TangentB.Z);
				}

				Vector3 Normal = TangentA.Cross(TangentB).Normalized();
				
				return Normal;
			}

			private Vector3 ComputeVertexVector(byte u, byte v, byte NormVer) // Calculates the transform or tangent vector of a given point on the bezier surface
			{
				// This code is going to be hard to read no matter what.
				// I've shorted down many of the names so that its compact, which makes it more readable in my opinion.
				Matrix tNB = NB; // temp NB
				Matrix tMB = MB; // Temp MB
				Matrix powerBasisU = PowerBasis(CNSize.x);
				Matrix powerBasisV = PowerBasis(CNSize.y);

				ByteVector2 LODedSVMSize = GetLODedSVMSize();

				float uF = (float)u / (float)(LODedSVMSize.x - 1);
				float vF = (float)v / (float)(LODedSVMSize.y - 1);

				if (NormVer == 1) { tNB = NBD; powerBasisU = PowerDiv(powerBasisU); } else
				if (NormVer == 2) { tMB = MBD; powerBasisV = PowerDiv(powerBasisV); }

				Matrix pU = PowsOfI(uF, powerBasisU).Transpose();
				Matrix pV = PowsOfI(vF, powerBasisV);
				
				Matrix cNX = new Matrix(CNSize.x, CNSize.y);
				Matrix cNY = new Matrix(CNSize.x, CNSize.y);
				Matrix cNZ = new Matrix(CNSize.x, CNSize.y);
				for (int i = 0; i < CNSize.x; i++)
				{
					for (int j = 0; j < CNSize.y; j++)
					{
						cNX[i, j] = CN[i, j].X;
						cNY[i, j] = CN[i, j].Y;
						cNZ[i, j] = CN[i, j].Z;
					}
				}

				Vector3 transform = new Vector3(0, 0, 0);
				
				Matrix pUProdTMB = pU.Product(tMB);
				Matrix tNBProdPV = tNB.Product(pV);


				// Note: Fix Do.x false and Do.z false, they don't work because they're set from 0,0 and not their control point.
				if (Do.x || NormVer != 0) { transform.X = pUProdTMB.Product(cNX).Product(tNBProdPV)[0,0]; } else { transform.X = ((float)SVMSize.x / ((float)LODedSVMSize.x - (float)1)) * (float)u; }
				if (Do.y || NormVer != 0) { transform.Y = pUProdTMB.Product(cNY).Product(tNBProdPV)[0,0]; } else { transform.Y = 0; }
				if (Do.z || NormVer != 0) { transform.Z = pUProdTMB.Product(cNZ).Product(tNBProdPV)[0,0]; } else { transform.Z = ((float)SVMSize.y / ((float)LODedSVMSize.y - (float)1)) * (float)v; }
				
				return transform;
			}

			private ByteVector2 GetLODedSVMSize()
			{ // Currently defunct method for Generating Different Levels of Detail based on distance.
				return new ByteVector2(SVMSize.x, SVMSize.y);

				/*Byte X = (byte)Math.Ceiling((float)SVMSize.x / (float)LOD);
				Byte Y = (byte)Math.Ceiling((float)SVMSize.y / (float)LOD);
				ByteVector2 LODedSVMSize = new ByteVector2(X, Y);
				return LODedSVMSize;*/
			}
			#endregion

			#region Triangles
				private Vector3[] WindTriangles(Vector3[,] STM)
				{
					int PackRATLen = ((SVMSize.x - 1) * (SVMSize.y - 1)) * 6;
					int n = 0;

					Vector3[] PackRAT = new Vector3[PackRATLen]; // Packed Reordered Array of Triangles
					for (int j = 0; j < SVMSize.y - 1; j++)
					{
						for (int i = 0; i < SVMSize.x - 1; i++)
						{
							PackRAT[n++] = STM[i, j];
							PackRAT[n++] = STM[i+1, j];
							PackRAT[n++] = STM[i+1, j+1];
							
							PackRAT[n++] = STM[i+1, j+1];
							PackRAT[n++] = STM[i, j+1];
							PackRAT[n++] = STM[i, j];
						}
					}
					return PackRAT;
				}
			#endregion

			#region Pows
				static Matrix PowsOfI(float i, Matrix Pows)
				{
					Matrix PowedI = new Matrix(Pows.GetLength(0), 1);
					for (int j = 0; j < Pows.GetLength(0); j++)
					{ 
						PowedI[j, 0] = (float)Math.Pow((double)i, (double)Pows[j, 0]);
					}
					return PowedI;
				}

				static Matrix PowerDiv(Matrix Pows)
				{
					for (int i = 1; i < Pows.GetLength(0); i++)
					{
						Pows[i, 0] = Pows[i - 1, 0];
					}
					return Pows;
				}
			#endregion
		}

		#region Bernsteins
			public static Matrix BernsteinPolynomial(int n)
			{
				Matrix B = new Matrix(n, n);
				n--;
				for (int i = 0; i <= n; i++)
				{
					for (int j = 0; j <= n; j++)
					{
						if (j >= i)
						{
							B[i, j] = (int)(BinomialCoefficient(n, i)*BinomialCoefficient(n-i, j-i)*(float)(Math.Pow(-1, j-i)));
						} else
						{
							B[i, j] = 0;
						}
					}
				}
				return B;
			}

			private Matrix DifferentiateBernstein(int n)
			{
				Matrix B = BernsteinPolynomial(n);
				Matrix DB = new Matrix(n, n);
				Matrix Pows = PowerBasis(n);
				int nreduc = n - 1;
				for (int i = 0; i < n; i++)
				{
					for (int j = 0; j < nreduc; j++)
					{
						DB[i, j] = B[i, j + 1] * Pows[j + 1, 0];
					}
				}
				return DB;
			}
			
			static Matrix PowerBasis(int n)
			{ // Creates an array with a number of indexes "n", where each value equals its index
				Matrix a = new Matrix(n, 1);
				for (int i = 0; i < n; i++)
				{
					a[i, 0] = i;
				}

				return a;
			}

			static private float BinomialCoefficient(int n, int k)
			{
					int a = Factorial(n);
					int b = Factorial(k)*Factorial(n-k);
					return a/b;
			}

			static private int Factorial(int n)
			{
				if (n == 0) { return 1; }

				int k = n;
				for (var i = n - 1; i > 0; i--)
				{
					k *= i;
				}
				return k;
			}
		#endregion
	}
}
