using Godot;
using System;
using System.Numerics;

namespace BezierSurfaces.Types
{
	namespace VectorVariants
	{
		namespace HalfVector
		{
			public struct HalfVector2
			{
				public HalfVector2(Half X, Half Y)
				{
					x = X;
					y = Y;
				}
				
				public Half x { get; }
				public Half y { get; }
				
				public override string ToString() => $"({x}, {y})";
			}
			
			public struct HalfVector3
			{
				public HalfVector3(Half X, Half Y, Half Z)
				{
					x = X;
					y = Y;
					z = Z;
				}
				
				public Half x { get; init; }
				public Half y { get; init; }
				public Half z { get; init; }
				
				public override string ToString() => $"({z}, {y}, {z})";
			}
		}

		namespace ByteVector
		{
			public struct ByteVector2
			{
				public ByteVector2(byte X, byte Y)
				{
					x = X;
					y = Y;
				}
				
				public byte x { get; }
				public byte y { get; }
				
				public override string ToString() => $"({x}, {y})";
			}
			
			public struct ByteVector3
			{
				public ByteVector3(byte X, byte Y, byte Z)
				{
					x = X;
					y = Y;
					z = Z;
				}
				
				public byte x { get; }
				public byte y { get; }
				public byte z { get; }
				
				public override string ToString() => $"({x}, {y}, {z})";
			}
		}

		namespace BitVector
		{
			public struct BitVector2
			{
				public BitVector2(bool X, bool Y)
				{
					x = X;
					y = Y;
				}
				
				public bool x { get; }
				public bool y { get; }
				
				public override string ToString() => $"({x}, {y})";
			}
			
			public struct BitVector3
			{
				public BitVector3(bool X, bool Y, bool Z)
				{
					x = X;
					y = Y;
					z = Z;
				}
				
				public bool x { get; init; }
				public bool y { get; init; }
				public bool z { get; init; }
				
				public override string ToString() => $"({z}, {y}, {z})";
			}
		}
	}
	namespace Matrix
	{
		public struct Matrix
		{
			
			public float[,] M;
			
			// Constructor
			public Matrix(int Rows, int Columns)
			{
				M = new float[Rows, Columns];
			}
			
			public Matrix Product(Matrix that)
			{
				this.Compatible(that);
				Matrix product = new Matrix(this.M.GetLength(0), that.M.GetLength(1));
				for (int i = 0; i < product.GetLength(0); i++)
				{
					for (int j = 0; j < product.GetLength(1); j++)
					{
						product.M[i, j] = this.DotProduct(that, i, j);
					}
				}
				return product;
			}
			
			public float DotProduct(Matrix that, int n, int m)
			{
				float dot = 0;
				for (int i = 0; i < this.M.GetLength(1); i++) 
				{
					dot += this.M[n, i] * that.M[i, m];
				}
				return dot;
			}
			
			public Matrix Transpose()
			{
				Matrix forklift = new Matrix(this.M.GetLength(1), this.M.GetLength(0));
				for (int i = 0; i < this.M.GetLength(1); i++)
				{
					for (int j = 0; j < this.M.GetLength(0); j++)
					{
						forklift.M[i, j] = this.M[j, i];
					}
				}
				return forklift;
			}
			
			private void Compatible(Matrix that)
			{
				if (this.M.GetLength(1) != that.M.GetLength(0))
				{
					throw new IncompatibleMatricies($"The provided matricies ({this.M.GetLength(0)}x{this.M.GetLength(1)}, {that.M.GetLength(0)}x{that.M.GetLength(1)}) are incompatible for multiplication.");
				}
			}
			
			public float this[int x, int y]
			{
				get => M[x, y];
				set => M[x, y] = value;
			}

			public void Print()
			{
				GD.Print("Printing Matrix");
				for (int i = 0; i < this.M.GetLength(0); i++)
				{
					string printer = "";
					for (int j = 0; j < this.M.GetLength(1); j++)
					{
						printer += this.M[i, j].ToString() + ", ";
					}
					GD.Print(printer);
				}
				
			}

			public int GetLength(int dim) => M.GetLength(dim);
			
			[Serializable]
			private class IncompatibleMatricies : Exception
			{
				public IncompatibleMatricies() { }
				
				public IncompatibleMatricies(string message) : base(message) { }
				
				public IncompatibleMatricies(string message, Exception inner) : base(message, inner) { }
			}
		}
	}
}
