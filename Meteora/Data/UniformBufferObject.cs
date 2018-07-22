using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vulkan;

namespace Meteora.Data
{
	public class UniformBufferObject
	{
		public float[,] model;
		public float[,] view;
		public float[,] proj;
	}
}
