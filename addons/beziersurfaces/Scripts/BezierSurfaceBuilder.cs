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
		Action<string> Print = (a) => GD.Print(a);
		
		private List<BezierSurface> SurfaceNetwork = new List<BezierSurface>();
		public List<List<ControlPoint>> ControlNetwork = new List<List<ControlPoint>>();
		public List<List<Vector3>> ControlNetworkPositions = new List<List<Vector3>>();
		
		[Export]
		public bool AutoUpdate = true; // Whether or not to continuously update the surfaces.

		[ExportGroup("Control Nodes")]
		
		[Export]
		public Mesh CNShape = new SphereMesh();
		
		[ExportSubgroup("Network Size")]
		
		[Export]
		public byte CNXSize = 4;
		[Export]
		public byte CNYSize = 4;
		
		private ByteVector2 CNSize = new ByteVector2(0, 0); // Control Net Size (Number of control points per surface)
		
		[ExportGroup("Vertex Map Size")]
		[Export]
		public byte SVMXSize = 32;
		[Export]
		public byte SVMYSize = 32;
		
		private ByteVector2 SVMSize = new ByteVector2(0, 0); // Surface Vertex Map Size (Vertex Density)
		
		[ExportGroup("Surface Size")]
		[Export]
		public byte SXSize = 32; // Surface X Size
		[Export]
		public byte SYSize = 32; // Surface X Size

		private ByteVector2 SSize = new ByteVector2(0, 0); // Surface Size (Nummber of game units a surface extends over, only applies to creation of control points.)
		
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

		private bool Loaded = false;

		public BezierSurfaceBuilder()
		{
			UpdateMaintenance();
		}
		
		public override void _EnterTree()
		{
			
			
		}

		public override void _Ready()
		{
			Print("Ready!");
			if (!Loaded)
			{
				Print("Loading!");
				Loaded = true;
				LoadSurfaces();
			}
		}

		public override void _Process(double delta)
		{
			if (AutoUpdate)
			{
				UpdateSurfaces();
			}
		}

		public void LoadSurfaces()
		{
			SurfaceNetwork = new List<BezierSurface>();
			ControlNetwork = new List<List<ControlPoint>>();
			ControlNetworkPositions = new List<List<Vector3>>();

			ReattainChildren();

			for (int i = 0; i < ControlNetwork.Count; i++)
			{
				for (int j = 0; j < ControlNetwork[i].Count; j++)
				{
					if (ControlNetwork[i][j].RotationDegrees.X == 90)
					{
						CreateSurface(new Vector2(i, j));
					}
				}
			}
		}

		public void ReattainChildren()
		{
			Godot.Collections.Array<Godot.Node> children = GetChildren();
			
			GD.Print("Children Count: " + children.Count);

			if(children.Count == 0)
			{
				ControlNetwork.Add(new List<ControlPoint>());
				ControlNetworkPositions.Add(new List<Vector3>());
				ControlNetwork[0].Add(ConstControlPoint(new Vector3(0, 0, 0)));
				ControlNetwork[0][0].Position = new Vector3(0, 0, 0);
				ControlNetworkPositions[0].Add(ControlNetwork[0][0].Position);
				return;
			}

			while (children.Count > 0)
			{
				for (int i = 0; i < children.Count; i++)
				{
					if (children[i] is ControlPoint Point)
					{
						if (Point.Loc.X < ControlNetwork.Count)
						{
							if ((float)ControlNetwork[(int)Point.Loc.X].Count == Point.Loc.Y)
							{
								ControlNetwork[(int)Point.Loc.X].Add(Point);
								ControlNetworkPositions[(int)Point.Loc.X].Add(Vector3.Zero);
								RemoveChild(Point);
								AddControlPoint(Point);
								children.RemoveAt(i);
							}
						}
						else if (Point.Loc.X == ControlNetwork.Count)
						{
							ControlNetwork.Add(new List<ControlPoint>());
							ControlNetworkPositions.Add(new List<Vector3>());
						}
					} else {
						children[i].QueueFree();
						children.RemoveAt(i);
					}
				}
			}
			children = GetChildren();
			GD.Print("Children Count: " + children.Count);
		}

		public void UpdateMaintenance()
		{
			bool RecalcNB = !(CNSize.X == CNXSize);
			bool RecalcMB = !(CNSize.Y == CNYSize);

			ByteVector2 NewCNSize = new ByteVector2(CNXSize, CNYSize);
			ByteVector2 NewSVMSize = new ByteVector2(SVMXSize, SVMYSize);
			ByteVector2 NewSSize = new ByteVector2(SXSize, SYSize);

			if (NewCNSize != CNSize)
			{

			}
			if (NewSVMSize != SVMSize)
			{

			}
			if (NewSSize != SSize)
			{

			}

			CNSize = NewCNSize;
			SVMSize = NewSVMSize;
			SSize = NewSSize;

			if (RecalcNB)
			{
				NB = BernsteinPolynomial(CNSize.X);
				NBD = DifferentiateBernstein(CNSize.X);
			}
			if (RecalcMB)
			{
				MB = BernsteinPolynomial(CNSize.Y).Transpose();
				MBD = DifferentiateBernstein(CNSize.Y).Transpose();
			}

			ControlPointSpacing = new Vector2((float)SSize.X/((float)CNSize.X - (float)1), (float)SSize.Y/((float)CNSize.Y - (float)1));
		}

		

		public async void UpdateAllSurfaces()
		{
			UpdateMaintenance();
			LoadPercent = 0;
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				SurfaceNetwork[i].ReloadSurface();
			}
		}

		public async void UpdateSurfaces()
		{
			UpdateMaintenance();
			LoadPercent = 0;
			List<ControlPoint> outdatedControlNodes = new List<ControlPoint>();
			for (int i = 0; i < ControlNetwork.Count; i++)
			{
				for (int j = 0; j < ControlNetwork[i].Count; j++)
				{
					if (ControlNetwork[i][j].Position != ControlNetworkPositions[i][j])
					{
						outdatedControlNodes.Add(ControlNetwork[i][j]);
						ControlNetworkPositions[i][j] = ControlNetwork[i][j].Position;
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
				ulong StartTime = Time.GetTicksUsec();
				outdatedSurfaces[i].ReloadSurface();
				ulong EndTime = Time.GetTicksUsec();
				GD.Print("Passed mesh creation in " + (EndTime - StartTime) + " μs");
			}
		}
		
		public void CreateSurfaceExternally(Vector2 Loc)
		{
			CreateSurface(Loc);
		}

		private BezierSurface CreateSurface(Vector2 Loc)
		{
			ControlNetwork[(int)Loc.X][(int)Loc.Y].HasSurface = true;
			for (int i = 0; i < Loc.X + CNSize.X; i++)
			{
				if (i == ControlNetwork.Count && i != Loc.X + CNSize.X)
				{
					ControlNetwork.Add(new List<ControlPoint>());
					ControlNetworkPositions.Add(new List<Vector3>());
				}
				for (int j = 0; j < Loc.Y + CNSize.Y; j++)
				{
					if (j == ControlNetwork[i].Count && j != Loc.Y + CNSize.Y)
					{
						AddPoint(i, j);
					}
				}
			}

			BezierSurface surface = new BezierSurface(this, Loc);

			SurfaceNetwork.Add(surface);
			
			return surface;
		}

		private void AddPoint(int i, int j)
		{
			ControlNetwork[i].Add(ConstControlPoint(new Vector3(i, 0, j)));
			ControlNetwork[i][j].Position = new Vector3((float)i*ControlPointSpacing.X, 0, (float)j*ControlPointSpacing.Y);
			ControlNetworkPositions[i].Add(ControlNetwork[i][j].Position);
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
					SurfaceNetwork[i].Patch.SetOwner(null);
					SurfaceNetwork[i].Patch.QueueFree();
					SurfaceNetwork.RemoveAt(i);
				}
			}
		}
		
		public ControlPoint ConstControlPoint(Vector3 Loc) // Construct Control Point
		{
			ControlPoint meshInstance = new ControlPoint();
			
			meshInstance.Mesh = CNShape;
			meshInstance.Name = NodePrefix + Loc.X.ToString() + "_" + Loc.Z.ToString();

			AddControlPoint(meshInstance);

			return meshInstance;
		}

		public void AddControlPoint(ControlPoint Point)
		{
			AddChild(Point, true, Node.InternalMode.Front);
			var theTree = GetTree().GetEditedSceneRoot();
			Point.SetOwner(theTree);
		}

		private struct BezierSurface
		{
			public Vector2 CNLoc = new Vector2(0, 0);
			
			public Byte LOD = 1;

			public MeshInstance3D Patch;

			readonly BezierSurfaceBuilder parent;

			private bool Loading = false;
			private bool QueuedForReload = false;

			#region Lambda Expressions
				List<List<ControlPoint>> ControlNetwork => parent.ControlNetwork;
				//ShaderMaterial NormalShower => parent.NormalShower;
				ByteVector2 CNSize => parent.CNSize;
				ByteVector2 SVMSize => parent.SVMSize;
				ByteVector2 SSize => parent.SSize;
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

				parent.AddChild(Patch, false, Node.InternalMode.Front); // Might cause issues that it's in this order.

				var theTree = parent.GetTree().GetEditedSceneRoot();
				Patch.SetOwner(theTree);
				
				ReloadSurface();
			}
			
			public async void ReloadSurface()
			{
				if (Loading)
				{
					QueuedForReload = true;
					return;
				}
				else
				{
					Loading = true;
				}

				ArrayMesh arrMesh = await CreateArrayMesh();
				

				ulong StartTime = Time.GetTicksUsec();
				Godot.Collections.Array<Node> children = Patch.GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					children[i].SetOwner(null);
					children[i].QueueFree();
				}

				

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

				ulong EndTime = Time.GetTicksUsec();
				GD.Print("Created new mesh in " + (EndTime - StartTime) + " μs");

				if (QueuedForReload)
				{
					QueuedForReload = false;
					ReloadSurface();	
				}
				else
				{
					Loading = false;
				}
			}

			private Vector3[,] GetControlNodes()
			{
				Vector3[,] CN = new Vector3[CNSize.X, CNSize.Y];
				for (int i = 0; i + CNLoc.X < ControlNetwork.Count && i < CNSize.X; i++)
				{
					for (int j = 0; j + CNLoc.Y < ControlNetwork[i + (int)CNLoc.X].Count && j < CNSize.Y; j++)
					{
						CN[i, j] = ControlNetwork[i + (int)CNLoc.X][j + (int)CNLoc.Y].Position;
					}
				}
				return CN;
			}
			
			public bool IsMyControlNode(Vector2 CPLoc)
			{
				float dx = CPLoc.X - CNLoc.X;
				float dy = CPLoc.Y - CNLoc.Y;
				return ((dx >= 0) && (dy >= 0) && (dx < CNSize.X) && (dy < CNSize.Y));
			}

			private MeshInstance3D CreatePatchMeshInstance()
			{
				MeshInstance3D Patch = new MeshInstance3D();

				Patch.Name = BezierPrefix + CNLoc.X.ToString() + "_" + CNLoc.Y.ToString();

				Patch.TopLevel = true;

				return Patch;
			}

			private async Task<ArrayMesh> CreateArrayMesh()
			{
				var LODedSVMSize = GetLODedSVMSize();

				var tCN = GetControlNodes();

				var tNB = NB;
				var tMB = MB;
				var tNBD = NBD;
				var tMBD = MBD;

				Vector3[,] STM = await Task.Run(() => GetSurfaceTransforms(tNB, tMB, tCN, LODedSVMSize));
				Vector3[,] SNM = await Task.Run(() => GetSurfaceNormals(tNB, tMB, tNBD, tMBD, tCN, LODedSVMSize));

				ArrayMesh ArrMesh = new ArrayMesh();

				var SurfaceArray = new Godot.Collections.Array();

				SurfaceArray.Resize((int)Mesh.ArrayType.Max);

				SurfaceArray[(int)Mesh.ArrayType.Vertex] = WindTriangles(STM);
				SurfaceArray[(int)Mesh.ArrayType.Normal] = WindTriangles(SNM);

				
				ArrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, SurfaceArray);

				//ArrMesh.SurfaceSetMaterial(0, NormalShower);


				return ArrMesh;
			}

			#region Create Array Mesh
			private static Vector3[,] GetSurfaceTransforms(Matrix NB, Matrix MB, Vector3[,] CN, ByteVector2 LODedSVMSize)
			{

				Vector3[,] STMForklift = new Vector3[LODedSVMSize.X, LODedSVMSize.Y];
				for (byte u = 0; u < LODedSVMSize.X; u++)
				{
					for (byte v = 0; v < LODedSVMSize.Y; v++)
					{
						STMForklift[u, v] = ComputeVertexVector(u, v, 0, NB, MB, CN, LODedSVMSize);
					}
				}
				return STMForklift;
			}
			
			private static Vector3[,] GetSurfaceNormals(Matrix NB, Matrix MB, Matrix NBD, Matrix MBD, Vector3[,] CN, ByteVector2 LODedSVMSize)
			{

				Vector3[,] SNMForklift = new Vector3[LODedSVMSize.X, LODedSVMSize.Y];				
				
				
				for (byte u = 0; u < LODedSVMSize.X; u++)
				{
					for (byte v = 0; v < LODedSVMSize.Y; v++)
					{
						SNMForklift[u, v] = ComputeVertexNormal(u, v, NB, MB, NBD, MBD, CN, LODedSVMSize);
					}
				}


				return SNMForklift;
			}

			private static Vector3 ComputeVertexNormal(byte u, byte v, Matrix NB, Matrix MB, Matrix NBD, Matrix MBD, Vector3[,] CN, ByteVector2 LODedSVMSize)
			{
				Vector3 TangentA = ComputeVertexVector(u, v, 1, NBD, MB, CN, LODedSVMSize);
				Vector3 TangentB = ComputeVertexVector(u, v, 2, NB, MBD, CN, LODedSVMSize);

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

			private static Vector3 ComputeVertexVector(byte u, byte v, int NormVer, Matrix tNB, Matrix tMB, Vector3[,] CN, ByteVector2 LODedSVMSize) // Calculates the transform or tangent vector of a given point on the bezier surface
			{
				// This code is going to be hard to read no matter what.
				// I've shorted down many of the names so that its compact, which makes it more readable in my opinion.
				Matrix powerBasisU = PowerBasis(CN.GetLength(0));
				Matrix powerBasisV = PowerBasis(CN.GetLength(1));

				float uF = (float)u / (float)(LODedSVMSize.X - 1);
				float vF = (float)v / (float)(LODedSVMSize.Y - 1);

				if (NormVer == 1) { powerBasisU = PowerDiv(powerBasisU); }
				if (NormVer == 2) { powerBasisV = PowerDiv(powerBasisV); }

				Matrix pU = PowsOfI(uF, powerBasisU).Transpose();
				Matrix pV = PowsOfI(vF, powerBasisV);
				
				Matrix cNX = new Matrix(CN.GetLength(0), CN.GetLength(1));
				Matrix cNY = new Matrix(CN.GetLength(0), CN.GetLength(1));
				Matrix cNZ = new Matrix(CN.GetLength(0), CN.GetLength(1));
				for (int i = 0; i < CN.GetLength(0); i++)
				{
					for (int j = 0; j < CN.GetLength(1); j++)
					{
						cNX[i, j] = CN[i, j].X;
						cNY[i, j] = CN[i, j].Y;
						cNZ[i, j] = CN[i, j].Z;
					}
				}

				Vector3 transform = new Vector3(0, 0, 0);
				
				Matrix pUProdTMB = pU.Product(tMB);
				Matrix tNBProdPV = tNB.Product(pV);

				transform.X = pUProdTMB.Product(cNX).Product(tNBProdPV)[0,0];
				transform.Y = pUProdTMB.Product(cNY).Product(tNBProdPV)[0,0]; 
				transform.Z = pUProdTMB.Product(cNZ).Product(tNBProdPV)[0,0];
				
				return transform;
			}

			private ByteVector2 GetLODedSVMSize()
			{ // Currently defunct method for Generating Different Levels of Detail based on distance.
				return new ByteVector2(SVMSize.X, SVMSize.Y);

				/*Byte X = (byte)Math.Ceiling((float)SVMSize.x / (float)LOD);
				Byte Y = (byte)Math.Ceiling((float)SVMSize.y / (float)LOD);
				ByteVector2 LODedSVMSize = new ByteVector2(X, Y);
				return LODedSVMSize;*/
			}
			#endregion

			#region Triangles
				private Vector3[] WindTriangles(Vector3[,] STM)
				{
					int PackRATLen = ((SVMSize.X - 1) * (SVMSize.Y - 1)) * 6;
					int n = 0;

					Vector3[] PackRAT = new Vector3[PackRATLen]; // Packed Reordered Array of Triangles
					for (int j = 0; j < SVMSize.Y - 1; j++)
					{
						for (int i = 0; i < SVMSize.X - 1; i++)
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
