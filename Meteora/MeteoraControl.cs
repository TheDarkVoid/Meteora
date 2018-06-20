using System;
using System.Windows.Forms;
using Vulkan.Windows;
using Vulkan;
using Meteora.View;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Meteora.Data;
using System.Threading;

namespace Meteora
{
	public class MeteoraControl : UserControl
	{
		public const string ENGINE_NAME = "Meteora";
		public readonly uint ENGINE_VERSION = Vulkan.Version.Make(0, 0, 1);

		public InstanceCreateData data;
		private Thread mainLoop;

		public MeteoraControl(MeteoraViewBase view, string appName)
		{
			//Default App Info
			var appInfo = new ApplicationInfo
			{
				ApplicationName = appName,
				ApplicationVersion = Vulkan.Version.Make(0, 0, 1),
				EngineName = ENGINE_NAME,
				EngineVersion = Vulkan.Version.Make(0, 0, 1),
				ApiVersion = Vulkan.Version.Make(1, 0, 0)
			};
			data.instance = CreateInstance(appInfo);
			data.view = view;
			data.appName = appName;
			data.control = this;
			this.BackColor = System.Drawing.Color.FromArgb(25, 0, 10);
		}

		public MeteoraControl(MeteoraViewBase view, ApplicationInfo appInfo)
		{
			//Ensure Engine Info
			appInfo.EngineName = ENGINE_NAME;
			appInfo.EngineVersion = ENGINE_VERSION;
			data.instance = CreateInstance(appInfo);
			data.view = view;
		}

		private Instance CreateInstance(ApplicationInfo appInfo)
		{
			var layerProperties = Commands.EnumerateInstanceLayerProperties();

			data.enabledDeviceExtensions = new string[] { "VK_KHR_swapchain" };

			//Debug Validation Layer
#if DEBUG
			data.enabledLayers = layerProperties.Any(l => l.LayerName == "VK_LAYER_LUNARG_standard_validation")
				? new[] { "VK_LAYER_LUNARG_standard_validation" }
				: new string[0];
#endif
			var instance = new Instance(new InstanceCreateInfo
			{
#if DEBUG
				EnabledExtensionNames = new string[] { "VK_KHR_surface", "VK_KHR_win32_surface", "VK_EXT_debug_report" },
				EnabledLayerNames = data.enabledLayers,
#else
				EnabledExtensionNames = new string[] { "VK_KHR_surface", "VK_KHR_win32_surface" },
#endif
				ApplicationInfo = appInfo
			});
#if DEBUG
			//Debug Callback
			SetupDebugCallback(instance);
			//instance.EnableDebug(DebugCallback);
#endif
			return instance;
		}

#if DEBUG
		private void SetupDebugCallback(Instance instance)
		{
			data.debugCallbackDelegate = DebugCallback;
			var createInfo = new DebugReportCallbackCreateInfoExt
			{
				Flags = DebugReportFlagsExt.Error | DebugReportFlagsExt.Warning,
				PfnCallback = Marshal.GetFunctionPointerForDelegate(data.debugCallbackDelegate)
			};
			data.debugCallback = instance.CreateDebugReportCallbackEXT(createInfo);
		}

		private Bool32 DebugCallback(DebugReportFlagsExt flags, DebugReportObjectTypeExt objectType, ulong objectHandle, IntPtr location, int messageCode, IntPtr layerPrefix, IntPtr message, IntPtr userData)
		{
			Debug.WriteLine($"{flags}: {Marshal.PtrToStringAnsi(message)}");
			Console.WriteLine($"{flags}: {Marshal.PtrToStringAnsi(message)}");
			return false;
		}
#endif

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			data.surface = data.instance.CreateWin32SurfaceKHR(new Win32SurfaceCreateInfoKhr
			{
				Hwnd = Handle,
				Hinstance = Process.GetCurrentProcess().Handle
			});

			var devices = data.instance.EnumeratePhysicalDevices();
			if (devices.Length == 0)
				throw new Exception("No devices found");
			data.physicalDevice = devices.FirstOrDefault(IsDeviceSuitable);
			if (data.physicalDevice == null)
				throw new Exception("No Suitable Device found");
			data.view.Initialize(data);
		}

		private bool IsDeviceSuitable(PhysicalDevice device)
		{
			var indices = FindQueueFamilies(device);
			var swapcahinAdequate = false;
			var (surfaceCapablities, formats, presentModes) = QuerySwapchainSupport(device);
			if(CheckExtensionSupport(device))
				swapcahinAdequate = formats.Length != 0 && presentModes.Length != 0;
			if(indices.isComplete() && swapcahinAdequate)
			{
				data.queueFamilyIndices = indices;
				data.surfaceCapabilities = surfaceCapablities;
				data.formats = formats;
				data.presentModes = presentModes;
				return true;
			}
			return false;
		}

		private bool CheckExtensionSupport(PhysicalDevice device)
		{
			var supportedExtensions = device.EnumerateDeviceExtensionProperties();
			return data.enabledDeviceExtensions.All(e => supportedExtensions.Any(se => se.ExtensionName == e));
		}

		private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
		{
			var queueFamilyProperties = device.GetQueueFamilyProperties();
			var queueFamilyIndices = new QueueFamilyIndices();
			for (int queueFamilyUsedIndex = 0; queueFamilyUsedIndex < queueFamilyProperties.Length; queueFamilyUsedIndex++)
			{
				//Check Present Support
				if (device.GetSurfaceSupportKHR((uint)queueFamilyUsedIndex, data.surface))
					queueFamilyIndices.PresentFamily = queueFamilyUsedIndex;
				//Check Graphics Support
				if (queueFamilyProperties[queueFamilyUsedIndex].QueueFlags.HasFlag(QueueFlags.Graphics))
					queueFamilyIndices.GraphicsFamily = queueFamilyUsedIndex;
			}
			return queueFamilyIndices;
		}

		private (SurfaceCapabilitiesKhr surfaceCapablities, SurfaceFormatKhr[] formats, PresentModeKhr[] presentModes) QuerySwapchainSupport(PhysicalDevice device)
		{
			return (device.GetSurfaceCapabilitiesKHR(data.surface), device.GetSurfaceFormatsKHR(data.surface), device.GetSurfacePresentModesKHR(data.surface));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			StartMainLoop();
		}

		private void StartMainLoop()
		{
			var threadStart = new ThreadStart(MainLoop);
			mainLoop = new Thread(threadStart);
			mainLoop.Start();
		}

		public void OnClosing()
		{
			data.view.running = false;
			mainLoop.Join();
			data.view.device.WaitIdle();
		}

		public void MainLoop()
		{
			while (data.view.running)
			{
				data.view.DrawFrame();
			}
		}

		protected override void Dispose(bool disposing)
		{
			data.view.Dispose();
			base.Dispose(disposing);
		}
	}
}
