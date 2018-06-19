using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Meteora.View;
using Vulkan;

namespace Meteora.Data
{
	public struct InstanceCreateData
	{
		public Instance instance;
		public SurfaceKhr surface;
		public string[] enabledLayers;
		public string[] enabledExtensions;
		public string[] enabledDeviceExtensions;
		public QueueFamilyIndices queueFamilyIndices;
		public MeteoraViewBase view;
		public PhysicalDevice physicalDevice;
		public SurfaceCapabilitiesKhr surfaceCapabilities;
		public SurfaceFormatKhr[] formats;
		public PresentModeKhr[] presentModes;
		public DebugReportCallbackExt debugCallback;
		public Instance.DebugReportCallback debugCallbackDelegate;
		public MeteoraControl control;
	}
}
