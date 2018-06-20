using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meteora.Data
{
	public interface IVector2
	{
		float X { get; set; }
		float Y { get; set; }

		float[] Data { get; set; }
	}

	public interface IVector3 : IVector2
	{
		float Z { get; set; }
	}

	public interface IVector4 : IVector3
	{
		float W { get; set; }
	}

	public struct Vector2 : IVector2
	{
		public float X { get => Data[0]; set => Data[0] = value; }
		public float Y { get => Data[1]; set => Data[1] = value; }

		public float[] Data
		{
			get => data;
			set
			{
				if (value == null)
					throw new NullReferenceException("Cannot set to null");
				if (value.Length != 2)
					throw new IndexOutOfRangeException($"Input length({value.Length}) does not match 2");
				data = value;
			}
		}

		private float[] data;

		public Vector2(float x = 0, float y = 0)
		{
			data = new float[] { x, y };
		}

		public Vector2(float[] data)
		{
			if (data == null)
				throw new NullReferenceException("Cannot set to null");
			if (data.Length != 2)
				throw new IndexOutOfRangeException($"Input length({data.Length}) does not match 2");
			this.data = data;
		}

	}

	public struct Vector3 : IVector3
	{
		public float X { get => Data[0]; set => Data[0] = value; }
		public float Y { get => Data[1]; set => Data[1] = value; }
		public float Z { get => Data[2]; set => Data[2] = value; }

		public float[] Data
		{
			get => data;
			set
			{
				if (value == null)
					throw new NullReferenceException("Cannot set to null");
				if (value.Length != 3)
					throw new IndexOutOfRangeException($"Input length({value.Length}) does not match 3");
				data = value;
			}
		}

		private float[] data;

		public Vector3(float x = 0, float y = 0, float z = 0)
		{
			data = new float[] { x, y, z };
		}

		public Vector3(float[] data)
		{
			if (data == null)
				throw new NullReferenceException("Cannot set to null");
			if (data.Length != 3)
				throw new IndexOutOfRangeException($"Input length({data.Length}) does not match 3");
			this.data = data;
		}
	}

	public struct Vector4 : IVector4
	{
		public float X { get => Data[0]; set => Data[0] = value; }
		public float Y { get => Data[1]; set => Data[1] = value; }
		public float Z { get => Data[2]; set => Data[2] = value; }
		public float W { get => Data[3]; set => Data[3] = value; }

		public float[] Data
		{
			get => data;
			set
			{
				if (value == null)
					throw new NullReferenceException("Cannot set to null");
				if (value.Length != 4)
					throw new IndexOutOfRangeException($"Input length({value.Length}) does not match 4");
				data = value;
			}
		}

		private float[] data;

		public Vector4(float x = 0, float y = 0, float z = 0, float w = 0)
		{
			data = new float[] { x, y, z, w };
		}

		public Vector4(float[] data)
		{
			if (data == null)
				throw new NullReferenceException("Cannot set to null");
			if (data.Length != 4)
				throw new IndexOutOfRangeException($"Input length({data.Length}) does not match 4");
			this.data = data;
		}
	}
}
