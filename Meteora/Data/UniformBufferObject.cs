using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vulkan;
using GlmSharp;
using System.Runtime.InteropServices;

namespace Meteora.Data
{
	public class UniformBufferObject
	{
		public mat4 model;
		public mat4 view;
		public mat4 proj;

		public float[] Values => model.Values1D.Concat(view.Values1D).Concat(proj.Values1D).ToArray();
	}
}
