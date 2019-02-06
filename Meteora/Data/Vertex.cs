using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vulkan;

namespace Meteora.Data
{
	public struct Vertex
	{
		public const int SIZE = sizeof(float) * 8;

		public Vector3 position;
		public Vector3 color;
		public Vector2 texCoord;

		public float[] Data
		{
			get
			{
				return new float[]
				{
					position.Data[0],
					position.Data[1],
					position.Data[2],
					color.Data[0],
					color.Data[1],
					color.Data[2],
					texCoord.Data[0],
					texCoord.Data[1],
				};
			}
		}

		public Vertex(Vector3 position, Vector3 color, Vector2 texCoord)
		{
			this.position = position;
			this.color = color;
			this.texCoord = texCoord;
		}

		public Vertex(float[] position, float[] color, float[] texCoord)
		{
			this.position = new Vector3(position);
			this.color = new Vector3(color);
			this.texCoord = new Vector2(texCoord);
		}

		public static VertexInputBindingDescription[] GetBindingDescription()
		{
			return new VertexInputBindingDescription[] { new VertexInputBindingDescription
			{
				Binding = 0,
				Stride = SIZE,
				InputRate = VertexInputRate.Vertex
			}};
		}

		public static VertexInputAttributeDescription[] GetAttributeDescriptions()
		{
			var attributeDescriptions = new VertexInputAttributeDescription[]
			{
				//Position
				new VertexInputAttributeDescription
				{
					Binding = 0,
					Location = 0,
					Format = Format.R32G32B32Sfloat,
					Offset = 0
				},
				//Color
				new VertexInputAttributeDescription
				{
					Binding = 0,
					Location = 1,
					Format = Format.R32G32B32Sfloat,
					Offset = 3 * sizeof(float)
				},
				//Tex Coord
				
				new VertexInputAttributeDescription
				{
					Binding = 0,
					Location = 2,
					Format = Format.R32G32Sfloat,
					Offset = 6 * sizeof(float)
				}
			};
			return attributeDescriptions;
		}
	}
}
