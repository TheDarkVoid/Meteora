using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vulkan;

namespace Meteora.View
{
	public interface IMeteoraView
	{
		void DrawFrame();
		void Initialize(PhysicalDevice physicalDevice, SurfaceKhr surface);
	}
}
