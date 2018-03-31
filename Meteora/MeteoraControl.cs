using System;
using System.Windows.Forms;
using Vulkan.Windows;
using Vulkan;
using Meteora.View;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Meteora
{
	public class MeteoraControl : VulkanControl
	{
		private IMeteoraView _vulkanSample;
		private PhysicalDevice _physicalDevice;

		public MeteoraControl(IMeteoraView vulkanSample)
		{
			_vulkanSample = vulkanSample;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			_physicalDevice = Instance.EnumeratePhysicalDevices()[0];
			_vulkanSample.Initialize(_physicalDevice, Surface);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			_vulkanSample.DrawFrame();
		}
	}
}
