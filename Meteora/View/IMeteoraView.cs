using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Meteora.Data;
using Vulkan;

namespace Meteora.View
{
	public interface IMeteoraView : IDisposable
	{
		void DrawFrame();
		void Initialize(InstanceCreateData data);
	}
}
