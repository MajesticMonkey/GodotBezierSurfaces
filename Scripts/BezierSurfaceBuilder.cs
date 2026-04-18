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
		
		[ExportGroup("Saving")]
		[Export]
		public String SaveFolder = "res://addons/beziersurfaces/SurfaceSaves/";
		
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
				if(!LoadSurfaces() || GetChildren().Count != 0)
				{
					ControlNetwork.Add(new List<ControlPoint>());
					ControlNetworkPositions.Add(new List<Vector3>());
					ControlNetwork[0].Add(ConstControlPoint(new Vector3(0, 0, 0)));
					ControlNetwork[0][0].Position = new Vector3(0, 0, 0);
					ControlNetworkPositions[0].Add(ControlNetwork[0][0].Position);
				}
			}
		}

		public override void _ExitTree()
		{
			Godot.Collections.Array<Godot.Node> children = GetChildren();
			
			for (int i = 0; i < children.Count; i++)
			{
				RemoveChild(children[i]);
				children[i].SetOwner(null);
				children[i].Free();
			}
			SaveSurfaces();
		}

		public void SaveSurfaces()
		{
			
			int APSize = 0; // Gets the number of points for later
			int[] PackAPD = new int[ControlNetwork.Count]; // Packed Array of Point Depths
			for (int i = 0; i < ControlNetwork.Count; i++)
			{
				PackAPD[i] = ControlNetwork[i].Count;
				APSize += ControlNetwork[i].Count;
			}

			Vector3[] PackAP = new Vector3[APSize]; // Packed Array of Points
			
			int Progress = 0;
			for (int i = 0; i < ControlNetwork.Count; i++)
			{
				for (int j = 0; j < ControlNetwork[i].Count; j++)
				{
					PackAP[Progress] = ControlNetworkPositions[i][j];
					Progress++;
				}
			}

			List<Vector2> ListASL = new List<Vector2>(); // Packed Array of Surface Locations
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				bool AddASL = true;
				for (int j = 0; j < ListASL.Count; j++)
				{
					if (ListASL[j] == SurfaceNetwork[i].CNLoc)
					{
						AddASL = false;
					}
				}
				if (AddASL)
				{
					ListASL.Add(SurfaceNetwork[i].CNLoc);
				}
			}

			Vector2[] PackASL = ListASL.ToArray();
			


			using var PointDepthsFile = FileAccess.Open(SaveFolder + Name + "PointDepths.dat", FileAccess.ModeFlags.Write);
			using var ControlNetworkFile = FileAccess.Open(SaveFolder + Name + "ControlNetwork.dat", FileAccess.ModeFlags.Write);
			using var SurfaceLocationsFile = FileAccess.Open(SaveFolder + Name + "SurfaceLocations.dat", FileAccess.ModeFlags.Write);

			PointDepthsFile.StoreVar(PackAPD);
			ControlNetworkFile.StoreVar(PackAP);
			SurfaceLocationsFile.StoreVar(PackASL);
			
			PointDepthsFile.Close();
			ControlNetworkFile.Close();
			SurfaceLocationsFile.Close();
		}

		public bool LoadSurfaces()
		{
			SurfaceNetwork = new List<BezierSurface>();
			ControlNetwork = new List<List<ControlPoint>>();
			ControlNetworkPositions = new List<List<Vector3>>();

			Godot.Collections.Array<Godot.Node> children = GetChildren();
			
			for (int i = 0; i < children.Count; i++)
			{
				RemoveChild(children[i]);
				children[i].SetOwner(null);
				children[i].Free();
			}
			
			string NamePrefix = SaveFolder + Name;

			if (!FileAccess.FileExists(NamePrefix + "PointDepths.dat") ||
			!FileAccess.FileExists(NamePrefix + "ControlNetwork.dat") ||
			!FileAccess.FileExists(NamePrefix + "SurfaceLocations.dat"))
			{
				return false;
			}
			
			using var PointDepthsFile = FileAccess.Open(NamePrefix + "PointDepths.dat", FileAccess.ModeFlags.Read);
			using var ControlNetworkFile = FileAccess.Open(NamePrefix + "ControlNetwork.dat", FileAccess.ModeFlags.Read);
			using var SurfaceLocationsFile = FileAccess.Open(NamePrefix + "SurfaceLocations.dat", FileAccess.ModeFlags.Read);

			int[] PackAPD = (int[])PointDepthsFile.GetVar();
			Vector3[] PackAP = (Vector3[])ControlNetworkFile.GetVar();
			Vector2[] PackASL = (Vector2[])SurfaceLocationsFile.GetVar();

			PointDepthsFile.Close();
			ControlNetworkFile.Close();
			SurfaceLocationsFile.Close();

			int Progress = 0;

			for (int i = 0; i < PackAPD.Length; i++)
			{
				Print(i.ToString());
				if (i == ControlNetwork.Count)
				{
					ControlNetwork.Add(new List<ControlPoint>());
					ControlNetworkPositions.Add(new List<Vector3>());
				}
				for (int j = 0; j < PackAPD[i]; j++) {
					AddPoint(i, j);
					ControlNetwork[i][j].Position = PackAP[Progress];
					ControlNetworkPositions[i][j] = PackAP[Progress];
					Progress++;
				}
			}

			for (int i = 0; i < PackASL.Length; i++)
			{
				CreateSurface(PackASL[i]);
			}

			return true;
		}

		private void printVector3(Vector3 v)
		{
			Print("(" + v.X + ", " + v.Y + ", " + v.X + ")");
		}

		public void UpdateMaintenance()
		{
			Do = new BitVector3(DoX, DoY, DoZ);

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
			Do = new BitVector3(DoX, DoY, DoZ);
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				SurfaceNetwork[i].ReloadSurface(SurfaceNetwork.Count, i);
			}
		}

		public async void UpdateSurfaces()
		{
			UpdateMaintenance();
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
				outdatedSurfaces[i].ReloadSurface(outdatedSurfaces.Count, i);
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
			AddChild(meshInstance, true, Node.InternalMode.Front);
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

				parent.AddChild(Patch, false, Node.InternalMode.Front); // Might cause issues that it's in this order.

				var theTree = parent.GetTree().GetEditedSceneRoot();
				Patch.SetOwner(theTree);
				
				ReloadSurface();
			}
			
			public void ReloadSurface(int SurfaceCount = 1, int SurfaceIndex = 0)
			{
				Godot.Collections.Array<Node> children = Patch.GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					children[i].SetOwner(null);
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
				Vector3[,] CN = new Vector3[CNSize.X, CNSize.Y];
				for (int i = 0; i + CNLoc.X < ControlNetwork.Count && i < CNSize.X; i++)
				{
					for (int j = 0; j + CNLoc.Y < ControlNetwork[i + (int)CNLoc.X].Count && j < CNSize.Y; j++)
					{
						CN[i, j] = ControlNetwork[i + (int)CNLoc.X][j + (int)CNLoc.Y].Position;
						if (!Do.X) { CN[i, j].X = ((CNLoc.X + i) * ControlPointSpacing.X); }
						if (!Do.Y) { CN[i, j].Y = 0; }
						if (!Do.Z) { CN[i, j].Z = ((CNLoc.Y + j) * ControlPointSpacing.Y); }
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

				Vector3[,] STMForklift = new Vector3[LODedSVMSize.X, LODedSVMSize.Y];
				for (byte u = 0; u < LODedSVMSize.X; u++)
				{
					for (byte v = 0; v < LODedSVMSize.Y; v++)
					{
						STMForklift[u, v] = ComputeVertexVector(u, v, 0);

						int count = u * LODedSVMSize.Y + v;

						parent.LoadPercent =
						((float)SurfaceIndex/(float)SurfaceCount) +
						((float)count/((float)LODedSVMSize.X * (float)LODedSVMSize.Y * (float)SurfaceCount * (float)2));

						// The math for the progress bar isn't that hard to understand, just read it a couple times.
					}
				}
				return STMForklift;
			}
			
			private Vector3[,] GetSurfaceNormals(int SurfaceCount = 1, int SurfaceIndex = 0)
			{
				ByteVector2 LODedSVMSize = GetLODedSVMSize();

				Vector3[,] SNMForklift = new Vector3[SVMSize.X, SVMSize.Y];				
				
				float firsthalf = (float)1 / ((float)SurfaceCount * (float)2);
				
				for (byte u = 0; u < LODedSVMSize.X; u++)
				{
					for (byte v = 0; v < LODedSVMSize.Y; v++)
					{
						SNMForklift[u, v] = ComputeVertexNormal(u, v);

						int count = u * LODedSVMSize.Y + v;

						parent.LoadPercent =
						((float)SurfaceIndex/(float)SurfaceCount) + firsthalf +
						((float)count/((float)LODedSVMSize.X * (float)LODedSVMSize.Y * (float)SurfaceCount * (float)2));
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
				Matrix powerBasisU = PowerBasis(CNSize.X);
				Matrix powerBasisV = PowerBasis(CNSize.Y);

				ByteVector2 LODedSVMSize = GetLODedSVMSize();

				float uF = (float)u / (float)(LODedSVMSize.X - 1);
				float vF = (float)v / (float)(LODedSVMSize.Y - 1);

				if (NormVer == 1) { tNB = NBD; powerBasisU = PowerDiv(powerBasisU); } else
				if (NormVer == 2) { tMB = MBD; powerBasisV = PowerDiv(powerBasisV); }

				Matrix pU = PowsOfI(uF, powerBasisU).Transpose();
				Matrix pV = PowsOfI(vF, powerBasisV);
				
				Matrix cNX = new Matrix(CNSize.X, CNSize.Y);
				Matrix cNY = new Matrix(CNSize.X, CNSize.Y);
				Matrix cNZ = new Matrix(CNSize.X, CNSize.Y);
				for (int i = 0; i < CNSize.X; i++)
				{
					for (int j = 0; j < CNSize.Y; j++)
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
				if (Do.X || NormVer != 0) { transform.X = pUProdTMB.Product(cNX).Product(tNBProdPV)[0,0]; } else { transform.X = ((float)SVMSize.X / ((float)LODedSVMSize.X - (float)1)) * (float)u; }
				if (Do.Y || NormVer != 0) { transform.Y = pUProdTMB.Product(cNY).Product(tNBProdPV)[0,0]; } else { transform.Y = 0; }
				if (Do.Z || NormVer != 0) { transform.Z = pUProdTMB.Product(cNZ).Product(tNBProdPV)[0,0]; } else { transform.Z = ((float)SVMSize.Y / ((float)LODedSVMSize.Y - (float)1)) * (float)v; }
				
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
