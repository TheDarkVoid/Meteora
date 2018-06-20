using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vulkan;

namespace Meteora.Data
{
	public struct Vertex
	{
		public const int SIZE = sizeof(float) * 5;

		public Vector2 position;
		public Vector3 color;

		public float[] Data
		{
			get
			{
				return new float[]
				{
					position.Data[0],
					position.Data[1],
					color.Data[0],
					color.Data[1],
					color.Data[2],
				};
			}
		}

		public Vertex(Vector2 position, Vector3 color)
		{
			this.position = position;
			this.color = color;
		}

		public Vertex(float[] position, float[] color)
		{
			this.position = new Vector2(position);
			this.color = new Vector3(color);
		}

		public static VertexInputBindingDescription[] GetBindingDescription()
		{
			var bindingDescription = new VertexInputBindingDescription
			{
				Binding = 0,
				Stride = SIZE,
				InputRate = VertexInputRate.Vertex
			};
			return new VertexInputBindingDescription[] { bindingDescription };
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
					Format = Format.R32G32Sfloat,
					Offset = 0
				},
				//Color
				new VertexInputAttributeDescription
				{
					Binding = 0,
					Location = 1,
					Format = Format.R32G32B32Sfloat,
					Offset = 2 * sizeof(float)
				}
			};
			return attributeDescriptions;
		}
	}
}
